using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebimSDK
{
    public class WMBaseSession
    {
        public enum WMSessionError
        {
            WMSessionErrorUnknown,
            WMSessionErrorReinitRequired,
            WMSessionErrorServerNotReady,
            WMSessionErrorAccountBlocked,
            WMSessionErrorVisitorBanned,
            WMSessionErrorNetworkError,
            WMSessionErrorChatNotFound,             // to be removed
            WMSessionErrorNotConfigured,            // to be removed
            WMSessionErrorAttachmentTypeNotAllowed,
            WMSessionErrorAttachmentSizeExceeded,
            WMSessionErrorMessageSizeExceeded,
            WMSessionErrorResponseDataError,
        }

        public enum WMChatAttachmentImageType
        {
            WMChatAttachmenWMChatAttachmentImageJPEG,
            WMChatAttachmentImagePNG,
        }

        public enum WMOperatorRate
        {
            WMOperatorRateOneStar = -2,
            WMOperatorRateTwoStars = -1,
            WMOperatorRateThreeStars = 0,
            WMOperatorRateFourStars = 1,
            WMOperatorRateFiveStars = 2,
        }

        public string AccountName { get; set; }

        public Uri HostUri { get; set; }

        public string Location { get; set; }

        private string DomainURLFormat = "https://{0}.webim.ru";

        public WMBaseSession() { }

        public WMBaseSession(string account, string location)
        {
            AccountName = account;
            Location = location;
        }

        public WMBaseSession(Uri hostUri, string location)
        {
            HostUri = hostUri;
            Location = location;
        }

        public string Host()
        {
            if (HostUri != null)
            {
                return HostUri.ToString();
            }
            if (AccountName == null || AccountName.Length == 0)
            {
                return null;
            }
            return string.Format(DomainURLFormat, AccountName);
        }

        public Uri AttachmentUriForMessage(WMMessage message)
        {
            string attachmentPath = message.AttachmentPath;
            if (attachmentPath == null || attachmentPath.Length == 0)
            {
                return null;
            }
            else if (message.Status == WMMessage.WMMessageStatus.NotSent && message.Kind == WMMessage.WMMessageKind.WMMessageKindFileFromVisitor)
            {
                return new Uri(message.AttachmentPath);
            }
            else if (!message.isFileMessage())
            {
                return null;
            }

            Uri hostUri = new Uri(Host());
            return new Uri(hostUri, attachmentPath);
        }

        internal static WMSessionError ErrorFromString(string value)
        {
            if ("reinit-required".Equals(value))
            {
                return WMSessionError.WMSessionErrorReinitRequired;
            }
            else if ("server-not-ready".Equals(value))
            {
                return WMSessionError.WMSessionErrorServerNotReady;
            }
            else if ("account-blocked".Equals(value))
            {
                return WMSessionError.WMSessionErrorAccountBlocked;
            }
            else if ("not_allowed_file_type".Equals(value))
            {
                return WMSessionError.WMSessionErrorAttachmentTypeNotAllowed;
            }
            else if ("max_file_size_exceeded".Equals(value))
            {
                return WMSessionError.WMSessionErrorAttachmentSizeExceeded;
            }
            else if ("chat-not-found".Equals(value))
            {
                return WMSessionError.WMSessionErrorChatNotFound;
            }
            else if ("message-length-exceeded".Equals(value))
            {
                return WMSessionError.WMSessionErrorMessageSizeExceeded;
            }
            else if ("visitor_banned".Equals(value))
            {
                return WMSessionError.WMSessionErrorVisitorBanned;
            }
            return WMSessionError.WMSessionErrorUnknown;
        }
    }
}
