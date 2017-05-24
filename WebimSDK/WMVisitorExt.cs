using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace WebimSDK
{
    public class WMVisitorExt
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string CRC { get; set; }
        public string ICQ { get; set; }
        public string ProfileURL { get; set; }
        public string AvatarURL { get; set; }
        public string ID { get; set; }
        public string Login { get; set; }

        public WMVisitorExt()
        {

        }

        public WMVisitorExt(string displayName, string phone, string email, string crc)
        {
            Name = displayName;
            Phone = phone;
            Email = email;
            CRC = crc;
        }

        internal string JsonEncoded()
        {
            JsonObject jsonObject = new JsonObject();
            JsonMaybeAddValue(jsonObject, "display_name", Name);
            JsonMaybeAddValue(jsonObject, "phone", Phone);
            JsonMaybeAddValue(jsonObject, "email", Email);
            JsonMaybeAddValue(jsonObject, "crc", CRC);
            JsonMaybeAddValue(jsonObject, "icq", ICQ);
            JsonMaybeAddValue(jsonObject, "profile_url", ProfileURL);
            JsonMaybeAddValue(jsonObject, "display_name", Name);
            JsonMaybeAddValue(jsonObject, "avatar_url", AvatarURL);
            JsonMaybeAddValue(jsonObject, "id", ID);
            JsonMaybeAddValue(jsonObject, "login", Login);

            string returnValue = jsonObject.Stringify();
            return returnValue;
        }

        private void JsonMaybeAddValue(JsonObject jsonObject, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                jsonObject[key] = JsonValue.CreateStringValue(value);
            }
        }
    }
}
