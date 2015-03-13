﻿using System;

namespace GrooveCasterServer.Models
{
    public class SetupRequest
    {
        public String Username { get; set; }

        public String Password { get; set; }

        public String Title { get; set; }

        public String Description { get; set; }

        public String Tag { get; set; }
    }
}