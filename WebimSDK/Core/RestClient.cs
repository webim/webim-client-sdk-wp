using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.Foundation;
using Windows.System.Threading;
using System.Threading;

namespace WebimSDK
{
    class RestClient
    {
        private string baseUrlString;
        public string BaseUrlString {
            get
            {
                return baseUrlString;
            }
            set
            {
                baseUrlString = value;
                if (httpClient != null)
                {
                    httpClient.BaseAddress = new Uri(baseUrlString);
                }
            }
        }

        private HttpClient httpClient { get; set; }

        private const string APIDeltaPath       = "/l/v/delta";
        private const string APIActionPath      = "/l/v/action";
        private const string APIHistoryPath     = "/l/v/history";
        private const string APIUploadPath      = "/l/v/upload";
        
        public RestClient()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue() {
                NoCache = true,
                MaxAge = TimeSpan.MinValue
            };
        }

        public RestClient(string baseUrl) : this()
        {
            BaseUrlString = baseUrl;
        }

        #region Offline Session Calls

        public async Task<HttpResponseMessage> GetHistoryAsync(bool forced, SharedStorage storage) {
            var parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "since", storage.Revision.ToString());
            DictionaryMaybeAddValue(parameters, "visitor-id", storage.VisitorID);

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.GetAsync(APIHistoryPath + "?" + content.ReadAsStringAsync().Result);
            return response;
        }

        public async Task<HttpResponseMessage> SendMessageAsync(string message, string subject, string departmentKey, string chatID, string fileDescriptor, string clientID, SharedStorage storage)
        {
            var parameters = new Dictionary<string, string>();
            parameters["action"] = "chat.offline_message";
            parameters["visitor-fields"] = "{}"; // TODO: that must be real value
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "msg_text", message);
            DictionaryMaybeAddValue(parameters, "chat-id", chatID);
            DictionaryMaybeAddValue(parameters, "department-key", departmentKey);
            DictionaryMaybeAddValue(parameters, "subject", subject);
            DictionaryMaybeAddValue(parameters, "file-descs", String.Format("[{0}]", fileDescriptor));
            DictionaryMaybeAddValue(parameters, "client-message-id", clientID);

            var content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        public async Task<HttpResponseMessage> DeleteMessageAsync(string chatID, SharedStorage storage)
        {
            var parameters = new Dictionary<string, string>();
            parameters["action"] = "chat.delete";
            DictionaryMaybeAddValue(parameters, "chat-id", chatID);
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);

            var content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        public async Task<HttpResponseMessage> MarkChatAsReadAsync(string chatID, SharedStorage storage)
        {
            var parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "action", "chat.read_by_visitor");
            DictionaryMaybeAddValue(parameters, "chat-id", chatID);
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);

            var content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        public async Task<HttpResponseMessage> SendFileAsync(byte[] fileData, string fileName, string mime, SharedStorage storage)
        {
            if (storage.PageID == null)
            {
                throw new Exception("Missing page id");
            }
            if (storage.VisitSessionID == null)
            {
                throw new Exception("Missing visitor id");
            }

            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "webim_upload_file";
            }

            var parameters = new Dictionary<string, string>();
            parameters["chat-mode"] = "offline";
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "visit-session-id", storage.VisitSessionID);
            
            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);

            var multipartContent = new MultipartFormDataContent();
            ByteArrayContent byteContent = new ByteArrayContent(fileData);
            byteContent.Headers.Add("Content-Type", mime);
            multipartContent.Add(byteContent, "webim_upload_file", fileName);

            HttpResponseMessage response = await httpClient.PostAsync(APIUploadPath + "?" + content.ReadAsStringAsync().Result, multipartContent);
            return response;
        }

        public async Task<HttpResponseMessage> StartSessionAsync(SharedStorage storage)
        {
            var parameters = new Dictionary<string, string>();
            parameters["event"] = "init";
            parameters["since"] = "0";
            parameters["title"] = "Windows Client";
            DictionaryMaybeAddValue(parameters, "ts", DateTimeHelper.Timestamp().ToString());
            DictionaryMaybeAddValue(parameters, "location", storage.Location);
            DictionaryMaybeAddValue(parameters, "platform", string.IsNullOrEmpty(storage.Platform) ? "winphone" : storage.Platform);
            DictionaryMaybeAddValue(parameters, "visit-session-id", storage.VisitSessionID);
            DictionaryMaybeAddValue(parameters, "push-token", storage.Token);
            DictionaryMaybeAddValue(parameters, "visitor", storage.Visitor);
            DictionaryMaybeAddValue(parameters, "visitor-ext", storage.VisitorFields);

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.GetAsync(APIDeltaPath + "?" + content.ReadAsStringAsync().Result);
            if (response.IsSuccessStatusCode)
            {
                string responseString = await response.Content.ReadAsStringAsync();
            }
            return response;
        }
        #endregion

        #region Realtime Session Calls

        private CancellationTokenSource _DeltaCancelationTokenSource = null;

        public async Task<HttpResponseMessage> GetDeltaAsync(bool useComet, SharedStorage storage)
        {
            if (storage == null || string.IsNullOrEmpty(storage.PageID))
            {
                return null;
            }
            var parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "since", storage.Revision.ToString()); // TODO
            DictionaryMaybeAddValue(parameters, "ts", DateTimeHelper.Timestamp().ToString());
            DictionaryMaybeAddValue(parameters, "respond-immediately", useComet ? "false" : "true");
            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);

            string path = APIDeltaPath + "?" + content.ReadAsStringAsync().Result;
            _DeltaCancelationTokenSource = new CancellationTokenSource();
            HttpResponseMessage response = await httpClient.GetAsync(path, _DeltaCancelationTokenSource.Token);

            string value = response.Content.ReadAsStringAsync().Result; // TODO: remove, for debug

            return response;
        }

        public void CancelDeltaRequest()
        {
            if (_DeltaCancelationTokenSource == null)
            {
                return;
            }
            _DeltaCancelationTokenSource.Cancel();
            _DeltaCancelationTokenSource = null;
        }

        public async Task<HttpResponseMessage> RealtimeStartChatAsync(string clientSideId, SharedStorage storage)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "action", "chat.start");
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "client-side-id", clientSideId);

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        public async Task<HttpResponseMessage> RealtimeCloseChatAsync(SharedStorage storage)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "action", "chat.close");
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        public async Task<HttpResponseMessage> RealtimeMarkChatAsReadAsync(SharedStorage storage)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "action", "chat.read_by_visitor");
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        public async Task<HttpResponseMessage> RealtimeChatSendMessageAsync(string message, string clientSideId, SharedStorage storage)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "action", "chat.message");
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "message", message);
            DictionaryMaybeAddValue(parameters, "client-side-id", clientSideId);

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        public async Task<HttpResponseMessage> RealtimeChatSetComposingMessageAsync(bool isComposing, bool draftChanged, string draft, SharedStorage storage)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "action", "chat.visitor_typing");
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "typing", isComposing ? "true" : "false");
            if (draftChanged)
            {
                if (string.IsNullOrEmpty(draft))
                {
                    DictionaryMaybeAddValue(parameters, "del-message-draft", "true");
                }
                else
                {
                    DictionaryMaybeAddValue(parameters, "message-draft", draft);
                }
            }

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        public async Task<HttpResponseMessage> RealtimeChatSendFileAsync(byte[] fileData, string fileName, string mimeType, string clientSideId, SharedStorage storage)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = "webim_upload_file";
            }

            var parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "client-side-id", clientSideId);

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            
            ByteArrayContent byteContent = new ByteArrayContent(fileData);
            byteContent.Headers.Add("Content-Type", mimeType);

            var multipartContent = new MultipartFormDataContent();
            multipartContent.Add(byteContent, "webim_upload_file", fileName);

            HttpResponseMessage response = await httpClient.PostAsync(APIUploadPath + "?" + content.ReadAsStringAsync().Result, multipartContent);
            return response;
        }

        public async Task<HttpResponseMessage> RealtimeChatSendImageAsync(byte[] imageData, WMSession.WMChatAttachmentImageType type, string clientSideId, SharedStorage storage)
        {
            string mime = type == WMBaseSession.WMChatAttachmentImageType.WMChatAttachmentImagePNG ? "image/png" : "image/jpeg";
            return await RealtimeChatSendFileAsync(imageData, null, mime, clientSideId, storage);
        }

        public async Task<HttpResponseMessage> RealtimeSetupPushTokenAsync(string pushToken, SharedStorage storage)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "action", "set_push_token");
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "push-token", pushToken);

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        private int OperatorRateToInt(WMBaseSession.WMOperatorRate rate)
        {
            switch (rate)
            {
                case WMBaseSession.WMOperatorRate.WMOperatorRateOneStar: return -2;
                case WMBaseSession.WMOperatorRate.WMOperatorRateTwoStars: return -1;
                case WMBaseSession.WMOperatorRate.WMOperatorRateThreeStars: return 0;
                case WMBaseSession.WMOperatorRate.WMOperatorRateFourStars: return 1;
                case WMBaseSession.WMOperatorRate.WMOperatorRateFiveStars: return 2;
            }
            return 5;
        }

        public async Task<HttpResponseMessage> RealtimeRateOeratorWithRate(string authorID, WMBaseSession.WMOperatorRate rate, SharedStorage storage)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            DictionaryMaybeAddValue(parameters, "action", "chat.operator_rate_select");
            DictionaryMaybeAddValue(parameters, "page-id", storage.PageID);
            DictionaryMaybeAddValue(parameters, "visit-session-id", storage.VisitSessionID);
            DictionaryMaybeAddValue(parameters, "operator-id", authorID);
            DictionaryMaybeAddValue(parameters, "rate", OperatorRateToInt(rate).ToString());

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await httpClient.PostAsync(APIActionPath, content);
            return response;
        }

        #endregion

        private void DictionaryMaybeAddValue(Dictionary<string, string> dictionary, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                dictionary[key] = value;
            }
        }
    }
}
