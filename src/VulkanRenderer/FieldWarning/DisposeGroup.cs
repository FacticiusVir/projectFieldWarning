using System;
using System.Collections.Generic;

namespace FieldWarning
{
    public class DisposeGroup
        : IDisposable
    {
        private readonly IEnumerable<IDisposable> items;

        public DisposeGroup(params IDisposable[] items)
            : this((IEnumerable<IDisposable>)items)
        {
        }

        public DisposeGroup(IEnumerable<IDisposable> items)
        {
            this.items = items;
        }

        public void Dispose()
        {
            foreach (var item in this.items)
            {
                item.Dispose();
            }
        }
    }
}
