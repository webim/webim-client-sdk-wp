using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WebimSDK;

namespace Webim_Client.DataModel
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
                OnPropertyChanged();
            }
        }

        private string _timeStamp;
        public string TimeStamp
        {
            get
            {
                return _timeStamp;
            }
            set
            {
                _timeStamp = value;
                OnPropertyChanged();
            }
        }

        private WMMessage _webimMessage;
        public WMMessage WebimMessage
        {
            get
            {
                return _webimMessage;
            }
            private set
            {
                _webimMessage = value;
                ApplyWMMessage(value);
            }
        }

        public Message(WMMessage message)
        {
            WebimMessage = message;
        }

        private void ApplyWMMessage(WMMessage message)
        {
            _webimMessage = message;

            Text = message.Text;
            TimeStamp = message.Timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        #region Interface INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}
