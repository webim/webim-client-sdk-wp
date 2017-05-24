using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace WebimSDK
{
    public class WMAPIResponse<T>
    {
        public bool Successful { get; set; }
        public WMBaseSession.WMSessionError WebimError { get; set; }
        //
        // Summary:
        //      HttpResponseMessage's StatusCode
        //
        // Returns:
        //     Returns System.Net.HttpStatusCode.The status code of the HTTP response.
        public int StatusCode { get; set; }
        //
        // Summary:
        //     HttpResponseMessage's ReasonPhrase
        //
        // Returns:
        //     Returns System.String.The reason phrase sent by the server.
        public string ReasonPhrase { get; set; }
        public T ResponseData { get; set; }

        public WMAPIResponse(bool status, HttpResponseMessage responseMessage, WMBaseSession.WMSessionError error, T responseData)
        {
            Successful = status;
            WebimError = error;
            if (responseMessage != null)
            {
                StatusCode = (int)responseMessage.StatusCode;
                ReasonPhrase = responseMessage.ReasonPhrase;
            }
            ResponseData = responseData;
        }

        internal WMAPIResponse(WMAPIResponse<bool> other)
        {
            Successful = other.Successful;
            StatusCode = other.StatusCode;
            ReasonPhrase = other.ReasonPhrase;
            WebimError = other.WebimError;
        }

        internal WMAPIResponse(WMAPIResponse<string> other)
        {
            Successful = other.Successful;
            StatusCode = other.StatusCode;
            ReasonPhrase = other.ReasonPhrase;
            WebimError = other.WebimError;
        }
    }
}
