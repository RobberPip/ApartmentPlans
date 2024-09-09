using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace ApartmentPlans
{
    
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Main : IExternalCommand
    {
        public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
        {
            TaskDialog td = new TaskDialog("Вариант запуска")
            {
                MainContent = "Выберите вариант запуска плагина: ",
                CommonButtons = TaskDialogCommonButtons.Close
            };
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Основной");
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Модуль: выгрузка листов");
            TaskDialogResult result = td.Show();   
            switch (result)
            {
                case TaskDialogResult.Cancel:
                    return Result.Cancelled;
                case TaskDialogResult.CommandLink1:
                    MainWindow window = new MainWindow(commandData.Application);
                    window.Show();
                    break;
                case TaskDialogResult.CommandLink2:
                    ExportModuleWindow exportModuleWindow = new ExportModuleWindow(commandData.Application);
                    exportModuleWindow.Show();
                    break;
            }
            return Result.Succeeded;
        }
    }
    
}
