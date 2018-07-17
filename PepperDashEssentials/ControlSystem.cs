﻿using System;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Devices.Common;
using PepperDash.Essentials.DM;
using PepperDash.Essentials.Fusion;
using PepperDash.Essentials.Room.Cotija;

namespace PepperDash.Essentials
{
	public class ControlSystem : CrestronControlSystem
	{
        HttpLogoServer LogoServer;

		public ControlSystem()
			: base()
		{
			Thread.MaxNumberOfUserThreads = 400;
			Global.ControlSystem = this;
			DeviceManager.Initialize(this);        
		}

		/// <summary>
		/// Git 'er goin'
		/// </summary>
		public override void InitializeSystem()
		{
            DeterminePlatform();

            //CrestronConsole.AddNewConsoleCommand(s => GoWithLoad(), "go", "Loads configuration file",
            //    ConsoleAccessLevelEnum.AccessOperator);

            CrestronConsole.AddNewConsoleCommand(s =>
            {
                foreach (var tl in TieLineCollection.Default)
                    CrestronConsole.ConsoleCommandResponse("  {0}\r", tl);
            },
            "listtielines", "Prints out all tie lines", ConsoleAccessLevelEnum.AccessOperator);

			CrestronConsole.AddNewConsoleCommand(s =>
			{
				CrestronConsole.ConsoleCommandResponse
					("Current running configuration. This is the merged system and template configuration");
				CrestronConsole.ConsoleCommandResponse(Newtonsoft.Json.JsonConvert.SerializeObject
					(ConfigReader.ConfigObject, Newtonsoft.Json.Formatting.Indented));
			}, "showconfig", "Shows the current running merged config", ConsoleAccessLevelEnum.AccessOperator);

			CrestronConsole.AddNewConsoleCommand(s =>
				{
					CrestronConsole.ConsoleCommandResponse("This system can be found at the following URLs:\r" +
						"System URL:   {0}\r" +
						"Template URL: {1}", ConfigReader.ConfigObject.SystemUrl, ConfigReader.ConfigObject.TemplateUrl);
				}, "portalinfo", "Shows portal URLS from configuration", ConsoleAccessLevelEnum.AccessOperator);

            GoWithLoad();
		}

        /// <summary>
        /// Determines if the program is running on a processor (appliance) or server (XiO Edge).
        /// 
        /// Sets Global.FilePathPrefix based on platform
        /// </summary>
        public void DeterminePlatform()
        {
            Debug.Console(0, Debug.ErrorLogLevel.Notice, "Determining Platform....");

            string filePathPrefix;

            var dirSeparator = Global.DirectorySeparator;

            var version = Crestron.SimplSharp.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            var versionString = string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);

            string directoryPrefix;

            directoryPrefix = Crestron.SimplSharp.CrestronIO.Directory.GetApplicationRootDirectory();

            //directoryPrefix = "";

            if (CrestronEnvironment.DevicePlatform != eDevicePlatform.Server)
            {
                filePathPrefix = directoryPrefix + dirSeparator + "NVRAM" 
                    + dirSeparator + string.Format("program{0}", InitialParametersClass.ApplicationNumber) + dirSeparator;

                Debug.Console(0, Debug.ErrorLogLevel.Notice, "Starting Essentials v{0} on 3-series Appliance", versionString);
            }
            else
            {
                filePathPrefix = directoryPrefix + dirSeparator + "User" + dirSeparator;

                Debug.Console(0, Debug.ErrorLogLevel.Notice, "Starting Essentials v{0} on XiO Edge Server", versionString);
            }

            Global.SetFilePathPrefix(filePathPrefix);
        }

		/// <summary>
		/// Do it, yo
		/// </summary>
		public void GoWithLoad()
		{
			try
			{
                CrestronConsole.AddNewConsoleCommand(EnablePortalSync, "portalsync", "Loads Portal Sync",
                    ConsoleAccessLevelEnum.AccessOperator);

                //PortalSync = new PepperDashPortalSyncClient();
                Debug.Console(0, Debug.ErrorLogLevel.Notice, "Starting Essentials load from configuration");

				var filesReady = SetupFilesystem();
				if (filesReady)
				{
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Folder structure verified. Loading config...");
					if (!ConfigReader.LoadConfig2())
						return;

					Load();
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Essentials load complete\r" +
						"-------------------------------------------------------------");
				}
				else
				{
					Debug.Console(0,
						"------------------------------------------------\r" +
						"------------------------------------------------\r" +
						"------------------------------------------------\r" +
						"Essentials file structure setup completed.\r" +
						"Please load config, sgd and ir files and\r" +
						"restart program.\r" +
						"------------------------------------------------\r" +
						"------------------------------------------------\r" +
						"------------------------------------------------");
				}
            }
			catch (Exception e)
			{
				Debug.Console(0, "FATAL INITIALIZE ERROR. System is in an inconsistent state:\r{0}", e);
			}

		}

		/// <summary>
		/// Verifies filesystem is set up. IR, SGD, and program1 folders
		/// </summary>
		bool SetupFilesystem()
		{
			Debug.Console(0, "Verifying and/or creating folder structure");
			var configDir = Global.FilePathPrefix;
			var configExists = Directory.Exists(configDir);
			if (!configExists)
				Directory.Create(configDir);

			var irDir = Global.FilePathPrefix + "ir";
			if (!Directory.Exists(irDir))
				Directory.Create(irDir);

            var sgdDir = Global.FilePathPrefix + "sgd";
			if (!Directory.Exists(sgdDir))
				Directory.Create(sgdDir);

			return configExists;
		}

        public void EnablePortalSync(string s)
        {
            if (s.ToLower() == "enable")
            {
                CrestronConsole.ConsoleCommandResponse("Portal Sync features enabled");
            }
        }

		public void TearDown()
		{
			Debug.Console(0, "Tearing down existing system");
			DeviceManager.DeactivateAll();

			TieLineCollection.Default.Clear();

			foreach (var key in DeviceManager.GetDevices())
				DeviceManager.RemoveDevice(key);

			Debug.Console(0, "Tear down COMPLETE");
		}

		/// <summary>
		/// 
		/// </summary>
		void Load()
		{
			LoadDevices();
			LoadTieLines();
			LoadRooms();
			LoadLogoServer();

			DeviceManager.ActivateAll();
		}


		/// <summary>
		/// Reads all devices from config and adds them to DeviceManager
		/// </summary>
		public void LoadDevices()
		{
            // Build the processor wrapper class
            DeviceManager.AddDevice(new PepperDash.Essentials.Core.Devices.CrestronProcessor("processor"));

			foreach (var devConf in ConfigReader.ConfigObject.Devices)
			{

				try
				{
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "Creating device '{0}'", devConf.Key);
					// Skip this to prevent unnecessary warnings
                    if (devConf.Key == "processor")
                    {
                        if (devConf.Type.ToLower() != Global.ControlSystem.ControllerPrompt.ToLower())
                            Debug.Console(0,
                                "WARNING: Config file defines processor type as '{0}' but actual processor is '{1}'!  Some ports may not be available",
                                devConf.Type.ToUpper(), Global.ControlSystem.ControllerPrompt.ToUpper());

                        continue;
                    }

					// Try local factory first
					var newDev = DeviceFactory.GetDevice(devConf);

					// Then associated library factories
					if (newDev == null)
						newDev = PepperDash.Essentials.Devices.Common.DeviceFactory.GetDevice(devConf);
					if (newDev == null)
						newDev = PepperDash.Essentials.DM.DeviceFactory.GetDevice(devConf);
					if (newDev == null)
						newDev = PepperDash.Essentials.Devices.Displays.DisplayDeviceFactory.GetDevice(devConf);

					if (newDev != null)
						DeviceManager.AddDevice(newDev);
					else
                        Debug.Console(0, Debug.ErrorLogLevel.Notice, "ERROR: Cannot load unknown device type '{0}', key '{1}'.", devConf.Type, devConf.Key);
				}
				catch (Exception e)
				{
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "ERROR: Creating device {0}. Skipping device. \r{1}", devConf.Key, e);
				} 
			}
            Debug.Console(0, Debug.ErrorLogLevel.Notice, "All Devices Loaded.");

		}

		/// <summary>
		/// Helper method to load tie lines.  This should run after devices have loaded
		/// </summary>
		public void LoadTieLines()
		{
			// In the future, we can't necessarily just clear here because devices
			// might be making their own internal sources/tie lines

			var tlc = TieLineCollection.Default;
			//tlc.Clear();
			if (ConfigReader.ConfigObject.TieLines == null)
			{
				return;
			}

			foreach (var tieLineConfig in ConfigReader.ConfigObject.TieLines)
			{
				var newTL = tieLineConfig.GetTieLine();
				if (newTL != null)
					tlc.Add(newTL);
			}

            Debug.Console(0, Debug.ErrorLogLevel.Notice, "All Tie Lines Loaded.");

		}

		/// <summary>
		/// Reads all rooms from config and adds them to DeviceManager
		/// </summary>
		public void LoadRooms()
		{
			if (ConfigReader.ConfigObject.Rooms == null)
			{
				Debug.Console(0, Debug.ErrorLogLevel.Warning, "WARNING: Configuration contains no rooms");
				return;
			}

			foreach (var roomConfig in ConfigReader.ConfigObject.Rooms)
			{
				var room = roomConfig.GetRoomObject();
				if (room != null)
				{
                    if (room is EssentialsHuddleSpaceRoom)
                    {
                        DeviceManager.AddDevice(room);

                        Debug.Console(0, Debug.ErrorLogLevel.Notice, "Room is EssentialsHuddleSpaceRoom, attempting to add to DeviceManager with Fusion");
                        DeviceManager.AddDevice(new EssentialsHuddleSpaceFusionSystemControllerBase((EssentialsHuddleSpaceRoom)room, 0xf1));


                        Debug.Console(0, Debug.ErrorLogLevel.Notice, "Attempting to build Cotija Bridge...");
						// Cotija bridge
						var bridge = new CotijaEssentialsHuddleSpaceRoomBridge(room as EssentialsHuddleSpaceRoom);
						AddBridgePostActivationHelper(bridge); // Lets things happen later when all devices are present
						DeviceManager.AddDevice(bridge);

                        Debug.Console(0, Debug.ErrorLogLevel.Notice, "Cotija Bridge Added...");
                    }
                    else if (room is EssentialsHuddleVtc1Room)
                    {
                        DeviceManager.AddDevice(room);

                        Debug.Console(0, Debug.ErrorLogLevel.Notice, "Room is EssentialsHuddleVtc1Room, attempting to add to DeviceManager with Fusion");
                        DeviceManager.AddDevice(new EssentialsHuddleVtc1FusionController((EssentialsHuddleVtc1Room)room, 0xf1));
                    }					
                    else
                    {
                        Debug.Console(0, Debug.ErrorLogLevel.Notice, "Room is NOT EssentialsHuddleSpaceRoom, attempting to add to DeviceManager w/o Fusion");
                        DeviceManager.AddDevice(room);
                    }

				}
				else
                    Debug.Console(0, Debug.ErrorLogLevel.Notice, "WARNING: Cannot create room from config, key '{0}'", roomConfig.Key);
			}

            Debug.Console(0, Debug.ErrorLogLevel.Notice, "All Rooms Loaded.");

		}

		/// <summary>
		/// Helps add the post activation steps that link bridges to main controller
		/// </summary>
		/// <param name="bridge"></param>
		void AddBridgePostActivationHelper(CotijaBridgeBase bridge)
		{
			bridge.AddPostActivationAction(() =>
			{
				var parent = DeviceManager.AllDevices.FirstOrDefault(d => d.Key == "appServer") as CotijaSystemController;
				if (parent == null)
				{
					Debug.Console(0, bridge, "ERROR: Cannot connect bridge. System controller not present");
				}
				Debug.Console(0, bridge, "Linking to parent controller");
				bridge.AddParent(parent);
				parent.AddBridge(bridge);
			});
		}

		/// <summary>
		/// Fires up a logo server if not already running
		/// </summary>
		void LoadLogoServer()
		{
			try
			{
                LogoServer = new HttpLogoServer(8080, Global.DirectorySeparator + "html" + Global.DirectorySeparator + "logo");
            }
			catch (Exception)
			{
				Debug.Console(0, Debug.ErrorLogLevel.Notice, "NOTICE: Logo server cannot be started. Likely already running in another program");
			}
		}
	}
}