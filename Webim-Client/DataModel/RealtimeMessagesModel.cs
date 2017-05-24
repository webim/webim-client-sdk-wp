using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WebimSDK;
using Windows.ApplicationModel;

namespace Webim_Client.DataModel
{
    public class RealtimeMessagesModel : INotifyPropertyChanged
    {
        private static RealtimeMessagesModel _model;
        public static RealtimeMessagesModel Model
        {
            get
            {
                if (_model == null)
                {
                    _model = new RealtimeMessagesModel();
                }
                return _model;
            }
        }

        private ObservableCollection<WMMessage> _messages;
        public ObservableCollection<WMMessage> Messages
        {
            get
            {
                return _messages;
            }
            set
            {
                _messages = value;
                OnPropertyChanged();
            }
        }

        public RealtimeMessagesModel()
        {
            Messages = new ObservableCollection<WMMessage>();
            if (DesignMode.DesignModeEnabled)
            {
                WMMessage fakeMessage = null;
                fakeMessage = new WMMessage();
                fakeMessage.Kind = WMMessage.WMMessageKind.WMMessageKindInfo;
                fakeMessage.Text = "Your pretty realtime chat";
                fakeMessage.Timestamp = DateTime.Now;
                fakeMessage.Ts = 0;
                Messages.Add(fakeMessage);

                fakeMessage = new WMMessage();
                fakeMessage.Kind = WMMessage.WMMessageKind.WMMessageKindOperator;
                fakeMessage.Text = "We are happy to see you here. What can I help you with?";
                fakeMessage.Timestamp = DateTime.Now;
                fakeMessage.Ts = 0;
                fakeMessage.SetSenderDetails("1", "Operator", null);
                Messages.Add(fakeMessage);

                fakeMessage = new WMMessage();
                fakeMessage.Kind = WMMessage.WMMessageKind.WMMessageKindVisitor;
                fakeMessage.Text = "Hello! How about lorem ipsum?";
                fakeMessage.Timestamp = DateTime.Now;
                fakeMessage.Ts = 1;
                Messages.Add(fakeMessage);

                fakeMessage = new WMMessage();
                fakeMessage.Kind = WMMessage.WMMessageKind.WMMessageKindVisitor;
                fakeMessage.Text = "Lorem ipsum dolor sit amet, no sanctus repudiandae his," +
                "ad altera eligendi euripidis has, ex sint adversarium sed. Est animal salutandi sadipscing cu." +
                "Vel minim dolores dissentiunt id. Vim vidit natum eligendi no, quas veritus ut eam.";
                fakeMessage.Timestamp = DateTime.Now;
                fakeMessage.Ts = 1;
                Messages.Add(fakeMessage);
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
