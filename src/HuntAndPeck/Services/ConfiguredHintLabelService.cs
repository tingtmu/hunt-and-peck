using System;
using System.Collections.Generic;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Services
{
    /// <summary>
    /// Generates labels from the currently configured alphabet, so alphabet changes apply to the next overlay
    /// </summary>
    internal sealed class ConfiguredHintLabelService : IHintLabelService
    {
        private readonly Func<string> _alphabet;

        /// <param name="alphabet">Returns the current, validated alphabet</param>
        public ConfiguredHintLabelService(Func<string> alphabet)
        {
            _alphabet = alphabet;
        }

        public IList<string> GetHintStrings(int count)
        {
            return new HintLabelService(_alphabet()).GetHintStrings(count);
        }
    }
}
