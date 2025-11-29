using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace main.domain.exception
{
    public class DomainException : Exception
    {
        public DomainException() { }

        public DomainException(string message) : base(message) { }
        public DomainException(string message, System.Exception innerException) : base(message, innerException) { }
        
    }
}