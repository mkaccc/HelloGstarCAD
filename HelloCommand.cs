using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using GrxCAD.Runtime;
using GrxCAD.ApplicationServices;
using HelloGstarCAD.Views;

[assembly: CommandClass(typeof(HelloGstarCAD.HelloCommand))]
namespace HelloGstarCAD
{
    public class HelloCommand
    {
        [CommandMethod("QEW")]
        public static void OpenBlockManager()
        {
            var doc = GrxCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                if (System.Windows.Application.Current == null)
                {
                    ed.WriteMessage("\n正在初始化WPF调度程序...\n");
                }

                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    try
                    {
                        ed.WriteMessage("\n正在创建插件窗口...\n");
                        var window = new BlockManagerWindow();
                        window.ShowDialog();
                        ed.WriteMessage("\n插件窗口已关闭。\n");
                    }
                    catch (System.Exception wpfEx)
                    {
                        ed.WriteMessage($"\n创建或显示窗口时出错: {wpfEx.Message}\n");
                        LogErrorToFile(wpfEx, "[WPF Dispatcher Error]");
                    }
                });

            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[严重错误] 打开插件界面时出错: {ex.Message}\n");
                LogErrorToFile(ex, "[Main Command Error]");
            }
        }

        private static void LogErrorToFile(System.Exception ex, string context)
        {
            try
            {
                string logPath = @"C:\Temp\GstarCAD_Error_Log.txt";
                string logMessage = $"\n[{DateTime.Now}] {context}\n" +
                                    $"消息: {ex.Message}\n" +
                                    $"堆栈: {ex.StackTrace}\n" +
                                    $"内部异常: {ex.InnerException?.Message}\n" +
                                    new string('-', 80) + "\n";
                
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch (System.Exception)
            {
                // 忽略文件记录错误
            }
        }
    }
}