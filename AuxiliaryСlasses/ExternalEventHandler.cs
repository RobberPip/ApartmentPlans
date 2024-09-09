using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApartmentPlans
{
    public class ExternalEventHandler : IExternalEventHandler
    {
        private Action _action;

        public void SetAction(Action action)
        {
            _action = action;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _action?.Invoke();
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                TaskDialog.Show("Error", $"Ошибка: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public string GetName()
        {
            return "External Event Handler";
        }
    }
}
