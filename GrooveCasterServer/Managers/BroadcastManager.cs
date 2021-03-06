﻿using System;
using System.Collections.Generic;
using GrooveCaster.Models;
using GS.Lib.Enums;
using GS.Lib.Events;
using GS.Lib.Models;
using ServiceStack.OrmLite;

namespace GrooveCaster.Managers
{
    public static class BroadcastManager
    {
        public static bool CreatingBroadcast { get; set; }

        static BroadcastManager()
        {
            CreatingBroadcast = false;
        }

        internal static void Init()
        {
            CreatingBroadcast = false;
            Program.Library.RegisterEventHandler(ClientEvent.BroadcastCreated, OnBroadcastCreated);
            Program.Library.RegisterEventHandler(ClientEvent.BroadcastCreationFailed, OnBroadcastCreationFailed);
            Program.Library.RegisterEventHandler(ClientEvent.ComplianceIssue, OnComplianceIssue);
            Program.Library.RegisterEventHandler(ClientEvent.PendingDestruction, OnPendingDestruction);
        }

        private static void OnPendingDestruction(SharkEvent p_SharkEvent)
        {
            if (Program.Library.Broadcast.CurrentBroadcastStatus != BroadcastStatus.Broadcasting)
                return;

            // Allows for custom queuing logic by Modules.
            if (!ModuleManager.OnFetchingNextSong())
                return;

            if (PlaylistManager.PlaylistActive && PlaylistManager.HasNextSong())
            {
                var s_SongID = PlaylistManager.DequeueNextSong();
                Program.Library.Broadcast.PlaySong(s_SongID,  Program.Library.Broadcast.AddSongs(new List<Int64> { s_SongID })[s_SongID]);
                return;
            }

            // Broadcast ran out of songs somehow; add and play a random song.
            var s_Random = new Random();
            var s_SongIndex = s_Random.Next(0, QueueManager.CollectionSongs.Count);
            var s_FirstSong = QueueManager.CollectionSongs[s_SongIndex];

            var s_QueueIDs = Program.Library.Broadcast.AddSongs(new List<Int64> { s_FirstSong });

            Program.Library.Broadcast.PlaySong(s_FirstSong, s_QueueIDs[s_FirstSong]);
        }

        private static void OnComplianceIssue(SharkEvent p_SharkEvent)
        {
            if (!SettingsManager.MobileCompliance())
            {
                DisableMobileCompliance();
                return;
            }

            // Allows for custom queuing logic by Modules.
            if (!ModuleManager.OnFetchingNextSong())
                return;

            // Try to play a new song.
            if (PlaylistManager.PlaylistActive && PlaylistManager.HasNextSong())
            {
                var s_SongID = PlaylistManager.DequeueNextSong();
                Program.Library.Broadcast.PlaySong(s_SongID, Program.Library.Broadcast.AddSongs(new List<Int64> { s_SongID })[s_SongID]);
                return;
            }

            var s_Random = new Random();
            var s_SongIndex = s_Random.Next(0, QueueManager.CollectionSongs.Count);
            var s_FirstSong = QueueManager.CollectionSongs[s_SongIndex];

            var s_QueueIDs = Program.Library.Broadcast.AddSongs(new List<Int64> { s_FirstSong });

            Program.Library.Broadcast.PlaySong(s_FirstSong, s_QueueIDs[s_FirstSong]);
        }

        internal static void CreateBroadcast()
        {
            if (CreatingBroadcast)
                return;

            if (QueueManager.CollectionSongs.Count < 2)
                return;

            CreatingBroadcast = true;

            Program.Library.Broadcast.CreateBroadcast(GetBroadcastName(), GetBroadcastDescription(), GetBroadcastCategoryTag());
        }

        public static String GetBroadcastName()
        {
            using (var s_Db = Database.GetConnection())
            {
                // TODO: Variable substitution.
                var s_Setting = s_Db.SingleById<CoreSetting>("bcname");
                return s_Setting == null ? "" : s_Setting.Value;
            }
        }

        public static String GetBroadcastDescription()
        {
            using (var s_Db = Database.GetConnection())
            {
                // TODO: Variable substitution.
                var s_Setting = s_Db.SingleById<CoreSetting>("bcdesc");
                return s_Setting == null ? "" : s_Setting.Value;
            }
        }

        public static CategoryTag GetBroadcastCategoryTag()
        {
            using (var s_Db = Database.GetConnection())
            {
                var s_Setting = s_Db.SingleById<CoreSetting>("bctag");

                if (s_Setting == null)
                    return new CategoryTag();

                var s_Tag = s_Setting.Value.Split(':');
                return new CategoryTag(s_Tag[0], s_Tag[1]);
            }
        }

        private static void OnBroadcastCreated(SharkEvent p_SharkEvent)
        {
            CreatingBroadcast = false;

            QueueManager.FetchCollectionSongs();
            QueueManager.ClearHistory();

            // Add two random songs to the collection.
            if (Program.Library.Queue.CurrentQueue.Count < 2)
            {
                var s_Random = new Random();
                var s_FirstSongIndex = s_Random.Next(0, QueueManager.CollectionSongs.Count);
                var s_SecondSongIndex = s_Random.Next(0, QueueManager.CollectionSongs.Count);

                var s_FirstSong = QueueManager.CollectionSongs[s_FirstSongIndex];
                var s_SecondSong = QueueManager.CollectionSongs[s_SecondSongIndex];

                while (s_SecondSong == s_FirstSong)
                {
                    s_SecondSongIndex = s_Random.Next(0, QueueManager.CollectionSongs.Count);
                    s_SecondSong = QueueManager.CollectionSongs[s_SecondSongIndex];
                }

                var s_QueueIDs = Program.Library.Broadcast.AddSongs(new List<Int64> {s_FirstSong, s_SecondSong});

                if (Program.Library.Broadcast.PlayingSongID == 0)
                    Program.Library.Broadcast.PlaySong(s_FirstSong, s_QueueIDs[s_FirstSong]);
            }
            else if (Program.Library.Broadcast.PlayingSongID == 0)
            {
                var s_Random = new Random();
                var s_SongIndex = s_Random.Next(0, QueueManager.CollectionSongs.Count);
                var s_FirstSong = QueueManager.CollectionSongs[s_SongIndex];

                var s_QueueIDs = Program.Library.Broadcast.AddSongs(new List<Int64> { s_FirstSong });

                Program.Library.Broadcast.PlaySong(s_FirstSong, s_QueueIDs[s_FirstSong]);
            }
            else if (Program.Library.Broadcast.PlayingSongID != 0)
            {
                QueueManager.UpdateQueue();
            }

            // Disable mobile compliance if needed.
            if (!SettingsManager.MobileCompliance())
                DisableMobileCompliance();
        }

        public static void DisableMobileCompliance()
        {
            Program.Library.Broadcast.DisableMobileCompliance();
        }

        private static void OnBroadcastCreationFailed(SharkEvent p_SharkEvent)
        {
            CreatingBroadcast = false;
        }

        public static bool AddGuest(String p_Username, Int64 p_UserID, VIPPermissions p_Permissions)
        {
            using (var s_Db = Database.GetConnection())
            {
                var s_Guest = s_Db.SingleById<SpecialGuest>(p_UserID);

                if (s_Guest != null)
                    return false;

                s_Guest = new SpecialGuest()
                {
                    Username = p_Username,
                    UserID = p_UserID,
                    Permissions = p_Permissions
                };

                s_Db.Insert(s_Guest);

                return true;
            }
        }

        public static bool RemoveGuest(Int64 p_UserID, out SpecialGuest p_Guest)
        {
            using (var s_Db = Database.GetConnection())
            {
                p_Guest = s_Db.SingleById<SpecialGuest>(p_UserID);

                if (p_Guest == null)
                    return false;

                s_Db.Delete(p_Guest);

                return true;
            }
        }

        public static bool RemoveGuest(Int64 p_UserID)
        {
            using (var s_Db = Database.GetConnection())
            {
                var s_Guest = s_Db.SingleById<SpecialGuest>(p_UserID);

                if (s_Guest == null)
                    return false;

                s_Db.Delete(s_Guest);

                return true;
            }
        }

        public static void UnguestAll()
        {
            foreach (var s_UserID in Program.Library.Broadcast.SpecialGuests)
            {
                Program.Library.Broadcast.RemoveSpecialGuest(s_UserID);
                return;
            }
        }

        public static SpecialGuest GetGuestForUserID(Int64 p_UserID)
        {
            using (var s_Db = Database.GetConnection())
                return s_Db.SingleById<SpecialGuest>(p_UserID);
        }

        public static bool HasActiveGuest(Int64 p_UserID)
        {
            return Program.Library.Broadcast.SpecialGuests.Contains(p_UserID);
        }

        public static void MakeGuest(SpecialGuest p_Guest)
        {
            Program.Library.Broadcast.AddSpecialGuest(p_Guest.UserID, p_Guest.Permissions);
        }

        public static void MakeGuest(Int64 p_UserID, VIPPermissions p_Permissions)
        {
            Program.Library.Broadcast.AddSpecialGuest(p_UserID, p_Permissions);
        }

        public static void Unguest(SpecialGuest p_Guest)
        {
            Program.Library.Broadcast.RemoveSpecialGuest(p_Guest.UserID);
        }

        public static void Unguest(Int64 p_UserID)
        {
            Program.Library.Broadcast.RemoveSpecialGuest(p_UserID);
        }

        public static void SetTitle(String p_Title)
        {
            Program.Library.Broadcast.UpdateBroadcastName(p_Title);
        }

        public static void SetDescription(String p_Description)
        {
            Program.Library.Broadcast.UpdateBroadcastDescription(p_Description);
        }

        public static bool CanUseCommands(SpecialGuest p_Guest)
        {
            if (p_Guest == null)
                return false;

            if (SettingsManager.CanCommandWithoutGuest())
                return true;

            return HasActiveGuest(p_Guest.UserID);
        }
    }
}
