using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using WebimSDK;

namespace Webim_Client.Selectors
{
    public class MessageOwnerDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate VisitorMessageDataTemplate { get; set; }
        public DataTemplate VisitorImageMessageDataTemplate { get; set; }
        public DataTemplate OperatorMessageDataTemplate { get; set; }
        public DataTemplate OperatorImageMessageDataTemplate { get; set; }
        public DataTemplate SystemMessageDataTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            WMMessage message = item as WMMessage;
            if (message == null)
            {
                return null;
            }
            switch (message.Kind)
            {
                case WMMessage.WMMessageKind.WMMessageKindVisitor: return VisitorMessageDataTemplate;
                case WMMessage.WMMessageKind.WMMessageKindFileFromVisitor: return VisitorImageMessageDataTemplate;
                case WMMessage.WMMessageKind.WMMessageKindOperator: return OperatorMessageDataTemplate;
                case WMMessage.WMMessageKind.WMMessageKindFileFromOperator: return OperatorImageMessageDataTemplate;
                default: return SystemMessageDataTemplate;
            }
        }
    }
}
