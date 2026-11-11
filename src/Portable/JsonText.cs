namespace DesktopPet
{
    /// <summary>
    /// JSON helpers shared by both builds.
    /// The .NET build uses System.Text.Json (part of the runtime, nothing to ship);
    /// the .NET Framework build keeps Newtonsoft.Json.
    /// Both accept comments, trailing commas and property names in any case.
    /// </summary>
    internal static class JsonText
    {
#if NETCORE
        private static readonly System.Text.Json.JsonSerializerOptions readOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static readonly System.Text.Json.JsonSerializerOptions writeOptions = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            // keep accents and symbols readable (no \uXXXX escapes) in files people edit by hand
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>Read a JSON text into an object.</summary>
        public static T Deserialize<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, readOptions);

        /// <summary>Write an object as indented, human readable JSON.</summary>
        public static string SerializeIndented(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), writeOptions);
#else
        /// <summary>Read a JSON text into an object.</summary>
        public static T Deserialize<T>(string json) => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);

        /// <summary>Write an object as indented, human readable JSON.</summary>
        public static string SerializeIndented(object value) => Newtonsoft.Json.JsonConvert.SerializeObject(value, Newtonsoft.Json.Formatting.Indented);
#endif
    }
}
