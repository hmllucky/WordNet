using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SentiWordFind
{
    
    public class Properties
    {
        //This is the data file that comes with Antelop Proxem API. This database is the adapted
        //version of Wordnet database.
        public static string ProxemDataFile = "C:\\Program Files\\Proxem\\Antelope\\data\\Proxem.Lexicon.dat";
        
        //This file contains the OpFinder words. The _test file is left over from residual code.
        public static string OpinionFinderFile = "./data/opFinder_train.txt";
        //This file contains the OpFinder words. The _test file is left over from residual code.
        public static string OpinionFinderFileAll = "./data/opFinder.txt";

        //Not used in current version. But referenced in one piece of code.
        public static string OpinionFinderTestFile = "./data/opFinder_test.txt";
    }
}
