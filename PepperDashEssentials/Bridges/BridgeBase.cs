﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.EthernetCommunication;

using Newtonsoft.Json;

using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Devices;
using PepperDash.Essentials.Core.Config;
using PepperDash.Essentials.Core.CrestronIO;
using PepperDash.Essentials.DM;

namespace PepperDash.Essentials.Bridges
{
    /// <summary>
    /// Base class for all bridge class variants
    /// </summary>
    public class BridgeBase : Device
    {
        public BridgeApi Api { get; private set; }

        public BridgeBase(string key) :
            base(key)
        {

        }
            
    }

    /// <summary>
    /// Base class for bridge API variants
    /// </summary>
    public abstract class BridgeApi : Device
    {
        public BridgeApi(string key) :
            base(key)
        {

        }

    }

    /// <summary>
    /// Bridge API using EISC
    /// </summary>
    public class EiscApi : BridgeApi
    {
        public EiscApiPropertiesConfig PropertiesConfig { get; private set; }

        public ThreeSeriesTcpIpEthernetIntersystemCommunications Eisc { get; private set; }


        public EiscApi(DeviceConfig dc) :
            base(dc.Key)
        {
            PropertiesConfig = JsonConvert.DeserializeObject<EiscApiPropertiesConfig>(dc.Properties.ToString());

            Eisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(PropertiesConfig.Control.IpIdInt, PropertiesConfig.Control.TcpSshProperties.Address, Global.ControlSystem);

            Eisc.SigChange += new Crestron.SimplSharpPro.DeviceSupport.SigEventHandler(Eisc_SigChange);

            Eisc.Register();

            AddPostActivationAction( () =>
            {
                Debug.Console(1, this, "Linking Devices...");

                foreach (var d in PropertiesConfig.Devices)
                {
                    var device = DeviceManager.GetDeviceForKey(d.DeviceKey);

                    if (device != null)
                    {
                        if (device is PepperDash.Essentials.Core.Monitoring.SystemMonitorController)
                        {
                            (device as PepperDash.Essentials.Core.Monitoring.SystemMonitorController).LinkToApi(Eisc, d.JoinStart, d.JoinMapKey);
                            continue;
                        }
                        else if (device is GenericComm)
                        {
                            (device as GenericComm).LinkToApi(Eisc, d.JoinStart, d.JoinMapKey);
                            continue;
                        }
                        else if (device is DmChassisController)
                        {
                            (device as DmChassisController).LinkToApi(Eisc, d.JoinStart, d.JoinMapKey);
                            continue;
                        }
                        else if (device is DmTxControllerBase)
                        {
                            (device as DmTxControllerBase).LinkToApi(Eisc, d.JoinStart, d.JoinMapKey);
                            continue;
                        }
                        else if (device is DmRmcControllerBase)
                        {
                            (device as DmRmcControllerBase).LinkToApi(Eisc, d.JoinStart, d.JoinMapKey);
                            continue;
                        }
                        else if (device is GenericRelayDevice)
                        {
                            (device as GenericRelayDevice).LinkToApi(Eisc, d.JoinStart, d.JoinMapKey);
                            continue;
                        }
                        else if (device is IDigitalInput)
                        {
                            (device as IDigitalInput).LinkToApi(Eisc, d.JoinStart, d.JoinMapKey);
                            continue;
                        }
                    }
                }

                Debug.Console(1, this, "Devices Linked.");
            });
        }

        /// <summary>
        /// Handles incoming sig changes
        /// </summary>
        /// <param name="currentDevice"></param>
        /// <param name="args"></param>
        void Eisc_SigChange(object currentDevice, Crestron.SimplSharpPro.SigEventArgs args)
        {
            if (Debug.Level >= 1)
                Debug.Console(1, this, "EiscApi change: {0} {1}={2}", args.Sig.Type, args.Sig.Number, args.Sig.StringValue);
            var uo = args.Sig.UserObject;
            if (uo is Action<bool>)
                (uo as Action<bool>)(args.Sig.BoolValue);
            else if (uo is Action<ushort>)
                (uo as Action<ushort>)(args.Sig.UShortValue);
            else if (uo is Action<string>)
                (uo as Action<string>)(args.Sig.StringValue);
        }
    }

    public class EiscApiPropertiesConfig
    {
        [JsonProperty("control")]
        public EssentialsControlPropertiesConfig Control { get; set; }

        [JsonProperty("devices")]
        public List<ApiDevice> Devices { get; set; }


        public class ApiDevice
        {
            [JsonProperty("deviceKey")]
            public string DeviceKey { get; set; }

            [JsonProperty("joinStart")]
            public uint JoinStart { get; set; }

            [JsonProperty("joinMapKey")]
            public string JoinMapKey { get; set; }
        }

    }


}