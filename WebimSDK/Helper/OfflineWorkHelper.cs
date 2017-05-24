using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using System.IO;

namespace WebimSDK
{
    class OfflineWorkHelper
    {
        // Return WMChat for new chats and WMMessage for new message in existed chat
        public static Object SendMessage(string text, WMChat chat, string subject, string departmentKey)
        {
            bool shouldCreateChat = chat == null;

            if (shouldCreateChat)
            {
                chat = CreateChat(subject, departmentKey);
            }
            var message = CreateMessage(text, chat, null, WMMessage.WMMessageKind.WMMessageKindVisitor);
            return shouldCreateChat ? (Object)chat : (Object)message;
        }

        public static async Task<Object> SendFileAsync(byte[] fileData, string fileName, string mimeType, WMChat chat, string subject, string departmentKey)
        {
            bool shouldCreateChat = chat == null;

            if (shouldCreateChat)
            {
                chat = CreateChat(subject, departmentKey);
            }
            var message = CreateMessage(null, chat, mimeType, WMMessage.WMMessageKind.WMMessageKindFileFromVisitor);
            message.FileName = fileName;

            try
            {
                string path = await SaveDataToFileAsync(message.Uid, fileData);
                message.AttachmentPath = path;
            }
            catch
            {
                return null;
            }
            return shouldCreateChat ? (Object)chat : (Object)message;
        }

        public static async Task<byte[]> LoadFileForMessageAsync(WMMessage message)
        {
            return await LoadDataFromFileAsync(message.Uid);
        }

        public static async Task DeleteFileForMessage(WMMessage message)
        {
            await DeleteTemporaryFileAsync(message.Uid);
            return;
        }

        private static WMChat CreateChat(string subject, string departmentKey)
        {
            var chat = new WMChat();
            chat.Uid = UIDGenerator.Next();
            chat.Subject = subject;
            chat.DepartmentKey = departmentKey;
            chat.Status = WMChat.WMChatStatus.NotSent;
            chat.Messages = new List<WMMessage>();
            chat.HasUnreadMessages = false; //< This chat/message is read, since it was posted by user himself
            return chat;
        }

        private static WMMessage CreateMessage(string text, WMChat chat, string mimeType, WMMessage.WMMessageKind kind)
        {
            var message = new WMMessage();
            message.Uid = UIDGenerator.Next();
            message.Text = text;
            message.MimeType = mimeType;
            message.Kind = kind;
            message.Status = WMMessage.WMMessageStatus.NotSent;
            message.Ts = DateTimeHelper.Timestamp();
            message.Timestamp = DateTimeHelper.DateTimeFromTimestamp(message.Ts);
            message.ClientRID = UIDGenerator.Next();
            if (chat != null)
            {
                message.ChatId = chat.Uid;
                chat.Messages.Add(message);
            }
            return message;
        }

        private static async Task<string> SaveDataToFileAsync(string filename, byte[] fileData)
        {
            string fileDir = "WebimTempUnsentFiles";

            StorageFolder folder = ApplicationData.Current.LocalFolder;
            StorageFolder dataFolder = await folder.CreateFolderAsync(fileDir, CreationCollisionOption.OpenIfExists);
            StorageFile file = await dataFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);

            using (var stream = await file.OpenStreamForWriteAsync())
            {
                await stream.WriteAsync(fileData, 0, fileData.Length);
            }
            return file.Path;
        }

        private static async Task<byte[]> LoadDataFromFileAsync(string filename)
        {
            string fileDir = "WebimTempUnsentFiles";

            StorageFolder folder = ApplicationData.Current.LocalFolder;
            StorageFolder dataFolder = await folder.GetFolderAsync(fileDir);
            StorageFile file = await dataFolder.GetFileAsync(filename);

            using (var stream = await file.OpenStreamForReadAsync())
            {
                using (var memStream = new MemoryStream())
                {
                    stream.CopyTo(memStream);
                    return memStream.ToArray();
                }
            }
        }

        private static async Task DeleteTemporaryFileAsync(string filename)
        {
            string fileDir = "WebimTempUnsentFiles";

            StorageFolder folder = ApplicationData.Current.LocalFolder;
            StorageFolder dataFolder = await folder.GetFolderAsync(fileDir);
            StorageFile file = await dataFolder.GetFileAsync(filename);
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
    }
}
