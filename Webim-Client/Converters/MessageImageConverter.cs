using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Data;

using WebimSDK;
using Webim_Client.Webim;

namespace Webim_Client.Converters
{
    public class MessageImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            WMMessage message = value as WMMessage;
            if (message == null || !message.isFileMessage())
            {
                return null;
            }
            WMBaseSession session = WebimController.Instance.RealtimeSession;
            if (session == null)
            {
                session = WebimController.Instance.OfflineSession;
            }
            if (session == null || !IsSupportedImage(message))
            {
                return null;
            }

            Uri path = session.AttachmentUriForMessage(message);

            switch (message.Status)
            {
                case WMMessage.WMMessageStatus.Sent: 
                    return new BitmapImage(path);
                case WMMessage.WMMessageStatus.NotSent:
                    return new BitmapImage(path);
            }
            return null;
        }

        private bool IsSupportedImage(WMMessage message)
        {
            foreach (var item in new List<String>() {".jpeg", ".jpg", ".png", ".bmp"})
            {
                bool isSupportedImage = ContainsAndEndsWithSubstring(message.AttachmentPath, item);
                if (isSupportedImage)
                {
                    return true;
                }
            }
            return false;
        }

        private bool ContainsAndEndsWithSubstring(string input, string substring)
        {
            try
            {
                int index = input.LastIndexOf(substring);
                return index + substring.Length == input.Length;
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
