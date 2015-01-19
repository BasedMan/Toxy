using System;
using Windows.UI.Notifications;

namespace Toxy.Common
{
    public class Toaster
    {
        private string _appId;

        public Toaster(string appId)
        {
            _appId = appId;
        }

        public void Show(string name, string message, string avatarPath = "")
        {
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText04);

            var stringElements = toastXml.GetElementsByTagName("text");
            stringElements[0].AppendChild(toastXml.CreateTextNode(name));
            stringElements[1].AppendChild(toastXml.CreateTextNode(message));

            var imageElements = toastXml.GetElementsByTagName("image");
            imageElements[0].Attributes.GetNamedItem("src").NodeValue = avatarPath;

            ToastNotification toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier(_appId).Show(toast);
        }
    }
}
