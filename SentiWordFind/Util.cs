using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Proxem.Antelope.Lexicon;
using Proxem.Antelope;
using System.Collections;
using System.IO;
using Proxem.Antelope.Tools.English;
using System.Data.SqlClient;
using System.Data;

namespace SentiWordFind
{
    public enum Polarity
    {
        Undefined,
        Postive,
        Negative,
        Neutral,
        Both

    }


    // Utility functions used across the project. 
    public class Util
    {
        public static PartOfSpeech ConvertFromOpFinderPOS(OpFinderPOS pos)
        {
            switch (pos)
            {
                case OpFinderPOS.Adjective:
                    return PartOfSpeech.Adjective;
                    
          case OpFinderPOS.Adverb:
                    return PartOfSpeech.Adverb;
                    
                case OpFinderPOS.Noun:
                    return PartOfSpeech.Noun;
                case OpFinderPOS.Verb:
                    return PartOfSpeech.Verb;
                    
                default:
                    throw new Exception("Unknown conversion from " + pos.ToString() + " requested");
            }

        }
    }

    public class LexiconDict
    {
        private static LexiconDict lexDict = new LexiconDict();
        private ILexicon lexicon = null;

        private LexiconDict()
        {
            this.lexicon = new Lexicon();
            lexicon.LoadDataFromFile(Properties.ProxemDataFile, null);
        }

        public static ILexicon getLexiconDict()
        {
            return lexDict.lexicon;
        }
    }

}
