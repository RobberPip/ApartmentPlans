using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApartmentPlans
{
    /// <summary>
    /// Класс для атрибута Json
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class JsonIncludeAttribute : Attribute
    {
    }
}
