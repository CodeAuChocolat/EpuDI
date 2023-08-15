using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EpuDI.Example
{
    public interface IService
    {
    }

    public class Service : IService
    {
    }

    public interface IAnotherService
    {
    }

    public class AnotherService : IAnotherService
    {
        public AnotherService(IService iNeedThisService)
        {
            
        }
    }

    public interface IInstanceCountService
    {
        public int Count { get; }
    }

    public class InstanceCountService : IInstanceCountService
    {
        private static int _count;
        
        public InstanceCountService()
        {
            _count++;
        }

        public int Count => _count;
    }
}
