using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WebimSDK;

namespace WebimUniversalApp.DataStructures
{
    public class Message : INotifyPropertyChanged
    {
        private string _Text;
        public string Text
        {
            get
            {
                return _Text;
            }
            set
            {
                _Text = value;
                OnPropertyChanged("Text");
            }
        }

        private string _From;
        public string From
        {
            get
            {
                return _From;
            }
            set
            {
                _From = value;
                OnPropertyChanged("From");
            }
        }

        private string _Date;
        public string Date
        {
            get
            {
                return _Date;
            }
            set
            {
                _Date = value;
                OnPropertyChanged("Date");
            }
        }

        private WMMessage _WebimMessage;
        public WMMessage WebimMessage
        {
            get
            {
                return _WebimMessage;
            }
            set
            {
                this.ApplyWMMessage(value, null);
            }
        }

        public Message(WMMessage message)
        {
            ApplyWMMessage(message, null);
        }

        public Message(WMMessage message, WMBaseSession session)
        {
            ApplyWMMessage(message, session);
        }

        private void ApplyWMMessage(WMMessage message, WMBaseSession session)
        {
            _WebimMessage = message;

            Text = message.Text;
            Date = message.Timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            From = message.Kind.ToString();
            if (message.isFileMessage() && session != null)
            {
                Text = session.AttachmentUriForMessage(message).ToString();
            }
        }

        #region Interface INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}
