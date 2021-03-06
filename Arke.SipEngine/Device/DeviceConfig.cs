﻿using System.Collections.Generic;
using Newtonsoft.Json;

namespace Arke.SipEngine.Device
{
    public class DeviceConfig
    {
        public int Id { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public string Name { get; set; }
        
        public DeviceType DeviceType { get; set; }
        public List<Feature> Features { get; set; }
        public List<Setting> Settings { get; set; }
        public List<Facility> Facilities { get; set; }
        public List<Workflow> Workflows { get; set; }
    }

    public class Workflow
    {
        private Dsl _value;
        public int Id { get; set; }
        public string Key { get; set; }
        public object Value {
            get => _value;
            set
            {
                var s = value as string;
                if (s != null)
                {
                    _value = JsonConvert.DeserializeObject<Dsl>(s);
                }
                else if (value is Dsl)
                {
                    _value = (Dsl) value;
                }
            } 
        }
    }
}
