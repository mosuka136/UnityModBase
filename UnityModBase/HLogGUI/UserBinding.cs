using System.Collections.Generic;
using UnityModBase.HGuiSpace;
using UnityModBase.HUserSpace;

namespace UnityModBase.HLogGUI
{
    public class UserBinding : UserBindingBase<EntryListBinding>
    {
        public UserBinding(IEnumerable<UserContext> contexts) : base(contexts, (service) => new EntryListBinding())
        {
            foreach (var context in contexts)
                AddData(context);
            UserManager.OnUserRegistered += AddData;
        }

        public override void AddData(UserContext context)
        {
            if (context == null || _user.ContainsKey(context.UserId))
                return;
            var list = new EntryListBinding();
            _user[context.UserId] = list;

            foreach (var log in context.Service.LogDatabase.Logs)
                list.AddEntry(new EntryBinding(log));
            context.Service.LogDatabase.OnLogAdded += l => list.AddEntry(new EntryBinding(l));
            context.Service.LogDatabase.OnLogRepeated += l => list.AddEntry(new EntryBinding(l));
        }
    }
}
