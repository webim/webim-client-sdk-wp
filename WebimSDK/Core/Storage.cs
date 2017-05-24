using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using System.IO;

namespace WebimSDK
{
    [DataContract]
    class Storage
    {
        [DataMember]
        public string Account { get; set; }

        [DataMember]
        public string Location { get; set; }
        
        [DataMember]
        public string Platform { get; set; }

        [DataMember]
        public string Token { get; set; }

        [DataMember]
        public double LastChangeTs { get; set; }

        [DataMember]
        public string Visitor { get; set; }  //Jsonized string

        [DataMember]
        public string VisitorID { get; set; }

        [DataMember]
        public string VisitSessionID { get; set; }

        [DataMember]
        public string PageID { get; set; }

        [DataMember]
        public List<WMChat> ChatsList { get; set; }

        public string VisitorFields { get; set; }

        public Storage()
        {
            ChatsList = new List<WMChat>();
        }

        public bool IsValidStorage()
        {
            return !String.IsNullOrEmpty(Account) && !String.IsNullOrEmpty(Token);
        }

        private static string DefaultFileName()
        {
            return "webimsdk";
        }

        public async Task SaveToFile()
        {
            string filename = DefaultFileName();

            var serializer = new DataContractSerializer(typeof(Storage));
            StorageFolder folder = ApplicationData.Current.LocalFolder;
            StorageFile file = await folder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);

            using (var stream = await file.OpenStreamForWriteAsync())
            {
                serializer.WriteObject(stream, this);
            }
        }

        public static async Task<Storage> LoadFromFile()
        {
            string filename = DefaultFileName();

            Storage storage = null;

            var deserializer = new DataContractSerializer(typeof(Storage));
            StorageFolder folder = ApplicationData.Current.LocalFolder;

            try
            {
                StorageFile file = await folder.GetFileAsync(filename);

                using (var stream = await file.OpenStreamForReadAsync())
                {
                    storage = (Storage)deserializer.ReadObject(stream);
                }
            }
            catch (Exception)
            {
                storage = new Storage(); // nop ;(
            }
            finally
            {
                if (storage == null)
                {
                    storage = new Storage();
                }
                else if (storage.ChatsList == null)
                {
                    storage.ChatsList = new List<WMChat>();
                }
            }

            return storage;
        }

        public async Task ClearStorageFile()
        {
            string filename = DefaultFileName();
            StorageFolder folder = ApplicationData.Current.LocalFolder;
            StorageFile file = await folder.GetFileAsync(filename);
            await file.DeleteAsync();
        }

        public void ClearMemoryData()
        {
            ChatsList = new List<WMChat>();
            PageID = String.Empty;
#if (false) // Login data
            Account; Location; Platform; Token;
#endif
            Visitor = String.Empty;
            VisitorID = String.Empty;
            VisitSessionID = String.Empty;
            LastChangeTs = 0;
        }

        public WMChat FindChatByID(string chatID)
        {
            if (ChatsList == null || ChatsList.Count == 0 || chatID.Length == 0)
                return null;
            var index = ChatsList.FindIndex(a => a.Uid.Equals(chatID));
            return index == -1 ? null : ChatsList[index];
        }
    }
}
