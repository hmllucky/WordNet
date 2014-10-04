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

//QuickGraph library. it is a conversion of C++ boost into C#
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Algorithms.ConnectedComponents;


namespace SentiWordFind
{

    class try2
    {
        //public static ILexicon lexDict = LexiconDict.getLexiconDict();
        //SynsetClassifier SynsetClassifier = new SynsetClassifier();

        ////Dictionary<string, WordInfo> wdict = new Dictionary<string, WordInfo>();//WC
        ////Dictionary<int, SynsetInfo> syndict = new Dictionary<int, SynsetInfo>();//SYN

        //private Hashtable synWmax = new Hashtable();//LIST


        Hashtable WC = new Hashtable();//全部
        Hashtable WordPolarity = new Hashtable();//已知
        Hashtable W1 = new Hashtable();
        Hashtable SYN = new Hashtable();//全部
        Hashtable S = new Hashtable();//未知POLARITY
        Hashtable SynsetPolarity = new Hashtable();//已知POLARITY
        //Hashtable SynPol = new Hashtable();
        Hashtable Httotalfrequence = new Hashtable();

        //SynsetClassifier synsetClassifier = new SynsetClassifier();
        //public static ILexicon lexDict = LexiconDict.getLexiconDict();

        public class SynNode
        {
            public int E;
            public int SynId;
        }
        public List<SynNode> SynNodes;

       public void output()
       {
           Console.Write("hello!");
       }
       public void Initialize()
       {
           OpinionFinderList.LoadOpinionFinderWordList(Properties.OpinionFinderFile, Properties.OpinionFinderTestFile, true);
           int count = OpinionFinderList.opTrainingWordList.Count;
           ArrayList keys = new ArrayList();
           keys.AddRange(OpinionFinderList.opTrainingWordList.Keys);

           WC = OpinionFinderList.opTrainingWordList;
           int start = 0;
           foreach (WordInfo w in WC.Values)
           {
               if ((start % 5) == 0)
               {
                   w.p = Polarity.Undefined;
                   W1.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, w);
               }
               else WordPolarity.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, w);
               start++;
           }
           //WordInfo w=WordPolarity.
           foreach (WordInfo w in WordPolarity.Values)
           {
               Console.Write(w.synInfo.lemma.Text);
               Console.Write(w.synInfo.lemma.Synset);
               Console.Write(w.ToString());
               Console.Write(w.synInfo.lemma.Synset.Lemmas);
           }
       }
    };
}