using Autodesk.Revit.DB;

namespace RevitAutoChecks.Revit
{
    /// <summary>ElementId в 2022–2023 — int (IntegerValue), с 2024 — long (Value).</summary>
    public static class ElementIds
    {
        public static long ToLong(ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId FromLong(long value)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(value);
#else
            return new ElementId(unchecked((int)value));
#endif
        }

        public static string ToStringValue(ElementId id) => ToLong(id).ToString();
    }
}
