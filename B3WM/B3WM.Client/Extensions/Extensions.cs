using B3WM.Shared.Entity;
using BlazorWorker.WorkerBackgroundService;

namespace B3WM.Client.Extensions
{
    public class KnownTypesJsonSerializer : SerializeLinqExpressionJsonSerializerBase
    {
        private static Type[]? _knownTypes;

        public override Type[] GetKnownTypes()
        {
            return new[] { typeof(Ticks2),  };
        }
    }
}
