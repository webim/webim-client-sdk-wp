using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace WebimSDK
{
    public class WMOperator
    {
        public string Name { get; set; }
        public string AvatarPath { get; set; }
        public string UID { get; set; }

        public WMOperator() { }

        public WMOperator(JsonValue operatorJsonValue) 
        {
            Initialize(operatorJsonValue);
        }

        internal void Initialize(JsonValue jsonValue)
        {
            JsonObject jsonObject = jsonValue.GetObject();
            IJsonValue value;

            if (jsonObject.TryGetValue("fullname", out value) && value.ValueType == JsonValueType.String)
            {
                Name = value.GetString();
            }
            if (jsonObject.TryGetValue("avatar", out value) && value.ValueType == JsonValueType.String)
            {
                AvatarPath = value.GetString();
            }
            if (jsonObject.TryGetValue("id", out value) && value.ValueType != JsonValueType.Null)
            {
                if (value.ValueType == JsonValueType.Number)
                {
                    UID = value.GetNumber().ToString();
                }
                else if (value.ValueType == JsonValueType.String)
                {
                    UID = value.GetString();
                }
            }
        }
    }
}
