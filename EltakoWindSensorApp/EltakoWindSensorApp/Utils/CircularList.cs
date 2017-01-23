using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EltakoWindSensorApp.Utils
{
    public class CircularList<T> : List<T>
    {
        private int _size;

		public CircularList(int size)
        {
            _size = size;
        }

		public new void Add(T item)
		{
			if (Count == _size)
			{
				RemoveAt(0);
			}
			base.Add(item);
		}
	}
}
