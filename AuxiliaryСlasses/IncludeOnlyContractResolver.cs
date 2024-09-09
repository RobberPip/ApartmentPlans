using ApartmentPlans;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Linq;
using System.Reflection;

public class IncludeOnlyContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        // Сериализовать только свойства с атрибутом JsonIncludeAttribute
        if (property.AttributeProvider.GetAttributes(typeof(JsonIncludeAttribute), true).Any())
        {
            property.Ignored = false; // Свойство будет сериализоваться
        }
        else
        {
            property.Ignored = true; // Свойство будет игнорироваться
        }

        return property;
    }
}


