using System.Collections.Generic;

namespace UnityModBase.HConfigGUI.Bindings
{
    public class SheetBinding
    {
        public List<TableBinding> Sheet { get; }

        public SheetBinding(ServiceRegistry service)
        {
            var tableBindings = new List<TableBinding>();

            foreach (var table in service.Config.Sheet)
            {
                var entryBindings = new List<IEntryBinding>();
                foreach (var entry in table.Value)
                    entryBindings.Add(new EntryBinding(service.ConfigManagerType, entry));
                tableBindings.Add(new TableBinding(entryBindings, table.Value.Name, table.Value.Description));
            }

            Sheet = tableBindings;
        }
    }
}
