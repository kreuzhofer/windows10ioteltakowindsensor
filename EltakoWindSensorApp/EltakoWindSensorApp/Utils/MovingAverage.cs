using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EltakoWindSensorApp.Utils
{
	public class MovingAverage
	{
		private CircularList<double> _items;

		public MovingAverage(int size)
		{
			_items = new CircularList<double>(size);
		}

		public void Add(double value)
		{
			_items.Add(value);
		}

		public double Average
		{
			get { return _items.Average(); }
		}
	}
}
