using System.Diagnostics;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace BatchResizer.Services;

public static class ToastNotificationService
{
    private const string AppId = "JezzWTF.BatchResizer";

    public static void ShowBatchComplete(int processed, int skipped, int errors, TimeSpan duration)
    {
        try
        {
            var xml = $"""
                <toast>
                  <visual>
                    <binding template="ToastGeneric">
                      <text>Batch Resizing Complete</text>
                      <text>{processed} resized · {skipped} skipped · {errors} errors · {duration.TotalSeconds:F1}s</text>
                    </binding>
                  </visual>
                </toast>
                """;

            var doc = new XmlDocument();
            doc.LoadXml(xml);
            ToastNotificationManager.CreateToastNotifier(AppId).Show(new ToastNotification(doc));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Toast] Failed: {ex.Message}");
        }
    }
}
