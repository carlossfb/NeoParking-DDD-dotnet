using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace src.domain.vo
{
    public class Plate
    {
        public string Document { get; private set; }

        private Plate(string document)
        {
            Document = document;
        }

        public static Plate Create(string document)
        {
            return new Plate(document);
        }
    }
}