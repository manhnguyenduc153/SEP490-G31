using System.Reflection;

namespace PRN232_be.DTO.Common
{
    public static class DmsPermissions
    {
        public static class Product
        {
            public const string Product_View = "Product.View";
            public const string Product_Create = "Product.Create";
            public const string Product_Edit = "Product.Edit";
            public const string Product_Delete = "Product.Delete";
        }

        public static List<string> GetAllPermissions()
        {
            var permissions = new List<string>();
            var nestedTypes = typeof(DmsPermissions).GetNestedTypes(BindingFlags.Public);
            foreach (var type in nestedTypes)
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                foreach (var field in fields)
                {
                    if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                    {
                        var value = field.GetRawConstantValue() as string;
                        if (value != null)
                        {
                            permissions.Add(value);
                        }
                    }
                }
            }
            return permissions;
        }
    }
}
