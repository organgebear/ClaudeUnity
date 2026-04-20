namespace ClaudeUnity
{
    /// <summary>
    /// Defers AssetDatabase.Refresh and script compilation until the conversation loop finishes,
    /// preventing domain reload from killing the async task mid-execution.
    /// </summary>
    public static class DeferredRefresh
    {
        private static bool _needsRefresh;
        private static bool _needsCompile;

        public static void Request()
        {
            _needsRefresh = true;
        }

        public static void RequestCompile()
        {
            _needsCompile = true;
        }

        /// <summary>
        /// Call this after the conversation loop ends to perform any pending refresh/compile.
        /// </summary>
        public static void FlushIfNeeded()
        {
            if (_needsRefresh)
            {
                _needsRefresh = false;
                UnityEditor.AssetDatabase.Refresh();
            }

            if (_needsCompile)
            {
                _needsCompile = false;
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            }
        }

        public static void Reset()
        {
            _needsRefresh = false;
            _needsCompile = false;
        }
    }
}
