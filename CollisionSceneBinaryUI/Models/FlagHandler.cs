using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollisionSceneBinaryUI.Models
{
    public class FlagHandler : ObservableObject
    {
        private ObservableCollection<int> properties = new ObservableCollection<int>();

        public ObservableCollection<int> Properties
        {
            get { return properties; }
            set { SetProperty(ref properties, value); }
        }

        public FlagHandler(ulong flag)
        {
            //load all enabled flag bits
            for (int i = 0; i < 64; i++)
            {
                if ((flag >> i & 1) != 0)
                    properties.Add(i);
            }
        }

        public void Set(int bit_pos, bool value = true)
        {
            if (value)
                Properties.Add(bit_pos);
            else if (Properties.Contains(bit_pos))
                Properties.Remove(bit_pos);
        }

        public ulong ToFlag()
        {
            ulong flag = 0;
            for (int j = 0; j < 64; j++)
            {
                if (Properties.Contains(j))
                    flag |= (1u << j);
            }
            return flag;
        }
    }
}
