﻿using System;
using System.Collections.Generic;
using Crestron.SimplSharp.CrestronIO;

using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core.Config;
using System.Text.RegularExpressions;

namespace PepperDash.Essentials
{
	/// <summary>
	/// Loads the ConfigObject from the file
	/// </summary>
	public class EssentialsConfig : BasicConfig
	{
        public string SystemUrl { get; set; }
        public string TemplateUrl { get; set; }

        public CotijaConfig Cotija { get; private set; }

        public string SystemUuid
        {
            get
            {
                var result = Regex.Match(SystemUrl, @"https?:\/\/.*\/templates\/(.*)#.*");

                string uuid = result.Groups[1].Value;

                return uuid;
            }
        }

        public string TemplateUuid
        {
            get
            {
                var result = Regex.Match(TemplateUrl, @"https?:\/\/.*\/templates\/(.*)#.*");

                string uuid = result.Groups[1].Value;

                return uuid;
            }
        }

		[JsonProperty("rooms")]
		public List<EssentialsRoomConfig> Rooms { get; private set; }
	}

	/// <summary>
	/// 
	/// </summary>
	public class SystemTemplateConfigs
	{
		public EssentialsConfig System { get; set; }

		public EssentialsConfig Template { get; set; }

        //public CotijaConfig Cotija { get; set; }
	}
}