using System;
using System.Reflection;

namespace ApartmentPlans
{
    public static class ObjectExtensions
    {
        public static void CopyPropertiesFrom<T>(this T target, T source)
        {
            if (source == null) return;
            if (target == null) return;

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (property.CanWrite)
                {
                    var value = property.GetValue(source);
                    property.SetValue(target, value);
                }
            }
        }
    }
}
