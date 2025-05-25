using System;
using System.IO;
using NitroxModel.Platforms.OS.Shared;

namespace NitroxModel.Helper
{
    public static class PirateDetection
    {
        public static bool HasTriggered { get; private set; }

        /// <summary>
        ///     Event that calls subscribers if the pirate detection triggered successfully.
        ///     New subscribers are immediately invoked if the pirate flag has been set at the time of subscription.
        /// </summary>
        public static event EventHandler PirateDetected
        {
            add
            {
                // Подписка есть, но событие никогда не вызовется, так как проверки отключены
                pirateDetected += value;

                // HasTriggered всегда false, поэтому вызова не будет
                if (HasTriggered)
                {
                    value?.Invoke(null, EventArgs.Empty);
                }
            }
            remove => pirateDetected -= value;
        }

        public static bool TriggerOnDirectory(string subnauticaRoot)
        {
            // Проверка отключена — всегда возвращаем false
            return false;
        }

        private static event EventHandler pirateDetected;

        private static bool IsPirateByDirectory(string subnauticaRoot)
        {
            // Проверка отключена — всегда возвращаем false
            return false;
        }

        private static void OnPirateDetected()
        {
            // Отключаем вызов события и не меняем флаг
            // pirateDetected?.Invoke(null, EventArgs.Empty);
            // HasTriggered = true;
        }
    }
}
