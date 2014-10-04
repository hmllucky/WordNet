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
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Algorithms.ConnectedComponents;

namespace SentiWordFind
{
    public class RunAnalysis
    {
        
        delegate bool determineSynsetPolarity(string knownWord, PartOfSpeech pos, Polarity knownPolarity);



        public static void Deduction(List<OpFinderWordInfo> listKnownWords,
                                     List<ILemma> listUnknownWords,
                                     List<ISynset> listSynsets, 
                                     out Hashtable newlyDiscoveredWords,
                                     out SynsetClassifier synClassifier)
        {
            Console.Out.WriteLine("==== In Component===========");
            Console.Out.WriteLine("#Known Words:" + listKnownWords.Count);
            Console.Out.WriteLine("#Unknown Words:" + listUnknownWords.Count);
            Console.Out.WriteLine("#Synsets:" + listSynsets.Count);
            Console.Out.WriteLine("==== Deduction Begins===========");

            ArrayList keys = new ArrayList();
            foreach(OpFinderWordInfo wInfo in listKnownWords)
            {
                keys.Add(wInfo.word + ":" + Util.ConvertFromOpFinderPOS(wInfo.pos));
                if (!OpinionFinderList.opTrainingWordList.Contains(wInfo.word + ":" + Util.ConvertFromOpFinderPOS(wInfo.pos)))
                {
                    OpinionFinderList.opTrainingWordList.Add(wInfo.word + ":" + Util.ConvertFromOpFinderPOS(wInfo.pos), wInfo);
                }
            }


            synClassifier = new SynsetClassifier();
            ArrayList wordListForSecondPass = new ArrayList();
            ArrayList wordListForSecondPass2 = new ArrayList();
            determineSynsetPolarity d = synClassifier.determineSynsetPolarity;
            ComputePolarity(synClassifier, wordListForSecondPass, keys, d);
            
            List<SimpleWord> list = new List<SimpleWord>();
            foreach (string words in wordListForSecondPass)
            {
                OpFinderWordInfo opwordInfo = (OpFinderWordInfo)OpinionFinderList.opTrainingWordList[words];
                SimpleWord wInfo = new SimpleWord(opwordInfo.word, Util.ConvertFromOpFinderPOS(opwordInfo.pos), opwordInfo.p);
                list.Add(wInfo);
            }

            //Apply Rule2
            d = synClassifier.determinSynsetPolarityByMinimalSet;
            ComputePolarity(synClassifier, wordListForSecondPass2, wordListForSecondPass, d);

            //Apply Hyponym Rule
            synClassifier.AssignPolarityAddHyponyms();

            //Now, that we applied all the rules to deduce polarity of synsets, lets
            //deduce the polarities of new words using those synsets and their polarities.
            newlyDiscoveredWords = new Hashtable();
            synClassifier.ComputePolarityOfWords(newlyDiscoveredWords);
            int count = newlyDiscoveredWords.Count;

            //update training list
            foreach (WordInfo w in newlyDiscoveredWords.Values)
            {
                OpFinderWordInfo wInfo = new OpFinderWordInfo();
                wInfo.word = w.word; wInfo.type = " "; wInfo.pos = (OpFinderPOS)w.synInfo.lemma.PartOfSpeech; wInfo.p = w.p;
                if (!OpinionFinderList.opTrainingWordList.Contains(wInfo.word + ":" + Util.ConvertFromOpFinderPOS(wInfo.pos)))
                {
                    OpinionFinderList.opTrainingWordList.Add(wInfo.word + ":" + Util.ConvertFromOpFinderPOS(wInfo.pos), wInfo);
                }
            }

            //Now we use this list, to deduce polarities for more words.
            //You may want to comment this if you want to test accuracy of rules. Any incorrectly
            //deduced words in this list may propagate the errors.
            Hashtable bootStrapList = new Hashtable(newlyDiscoveredWords);
            Dictionary<string, WordInfo> dict = new Dictionary<string, WordInfo>();
            int step = 1;

            //Now bootstrap, refere to previous note.
            //This while loop pretty much applies the set of rules we applied above.
            //The loop continues until no more words/synsets can be deduced.
            while (true)
            {
                wordListForSecondPass = new ArrayList();
                wordListForSecondPass2 = new ArrayList();
                d = synClassifier.determineSynsetPolarity;

                //Rule0 and Rule1
                ComputePolarity(synClassifier, wordListForSecondPass, bootStrapList.Keys, d, bootStrapList);
                List<SimpleWord> list2 = new List<SimpleWord>();
                foreach (string word in wordListForSecondPass)
                {
                    WordInfo wInfo = (WordInfo)bootStrapList[word];
                    list2.Add(new SimpleWord(wInfo.word, wInfo.synInfo.lemma.PartOfSpeech, wInfo.p));
                }

                list.AddRange(list2);
                d = synClassifier.determinSynsetPolarityByMinimalSet;
                //Rule 2
                ComputePolarity(synClassifier, wordListForSecondPass2, wordListForSecondPass, d, bootStrapList);

                //HyponymRule
                synClassifier.AssignPolarityAddHyponyms();

                //Compute the polority of the next set of new words, using the additional synsets 
                //identified in this pass.
                Hashtable nextList = synClassifier.ComputePolarityOfWords(newlyDiscoveredWords);

                //update training list
                foreach (WordInfo w in nextList.Values)
                {
                    OpFinderWordInfo wInfo = new OpFinderWordInfo();
                    wInfo.word = w.word; wInfo.type = " "; wInfo.pos = (OpFinderPOS)w.synInfo.lemma.PartOfSpeech; wInfo.p = w.p;
                    if (!OpinionFinderList.opTrainingWordList.Contains(wInfo.word + ":" + Util.ConvertFromOpFinderPOS(wInfo.pos)))
                    {
                        OpinionFinderList.opTrainingWordList.Add(wInfo.word + ":" + Util.ConvertFromOpFinderPOS(wInfo.pos), wInfo);
                    }
                }

                //quit, if this pass did not give us any new words.
                if (nextList.Count == 0)
                {
                    // newlyDiscoveredWords = bootStrapList;
                    break;
                }
                else
                    bootStrapList = nextList;

                step++;
            }

        }



        /// <summary>
        /// Method leaves one word out the the training list, and finds all polarities of all synsets and words using the
        /// rules. Finally, the method checks if the word left out was found in the deduced list and the trace is printed if the word was found.
        /// </summary>
        
        public static void RunLeaveOneOutFoldTest()
        {
            OpinionFinderList.LoadOpinionFinderWordList(Properties.OpinionFinderFile, Properties.OpinionFinderTestFile, true);
            
            int count = OpinionFinderList.opTrainingWordList.Count;
            ArrayList keys = new ArrayList();
            keys.AddRange(OpinionFinderList.opTrainingWordList.Keys);

            Hashtable testList = new Hashtable();
            
            for (int k = 0; k < count; k++)
            {
                //if ( ! ((string)keys[k]).Contains("bad")) continue;
                testList.Add(keys[k], OpinionFinderList.opTrainingWordList[keys[k]]);
                
                OpinionFinderList.opTrainingWordList.Remove(keys[k]);

                Hashtable newlyDiscoveredWords;
                ArrayList correctList;
                ArrayList incorrectList;
                Run(out newlyDiscoveredWords, out correctList, out incorrectList);
                
                Console.Write(k+1);
                Console.Write(", ");

                Console.Write(newlyDiscoveredWords.Count);
                Console.Write(", ");

                string miscInfo = string.Empty;

                WordInfo wInfo = (WordInfo) newlyDiscoveredWords[keys[k]];
                if ( wInfo != null )
                {
                    if ( wInfo.p == ((OpFinderWordInfo)testList[keys[k]]).p )
                        Console.Write("correct, ");
                    else
                    {
                        Console.Write("incorrect, ");
                       
                        // world reached through synset
                        // list of synsets the help 

                        string synsetInfo = string.Empty;

                        foreach (SynsetInfo sInfo in wInfo.synContribList)
                        {
                            SynsetInfo traceSynset = wInfo.synInfo.rootSynset;

                            synsetInfo = synsetInfo + "(" + sInfo.lemma + ";" + ((sInfo.rootSynset == null) ? sInfo.rootWord : "") + ";" + sInfo.polarity + ";" + sInfo.derivedCondition +  ")";

                            while (traceSynset != null)
                            {
                                synsetInfo = synsetInfo + "(" + traceSynset.lemma + ";" + ((traceSynset.rootSynset == null) ? traceSynset.rootWord : "") + ";" + traceSynset.polarity + ";" + traceSynset.derivedCondition + ")";
                                traceSynset = traceSynset.rootSynset;
                            }
                            synsetInfo = synsetInfo + ":";
                        }

                        miscInfo = wInfo.p.ToString() + "," + wInfo.derivedCondition + "," + synsetInfo;

                      }
                }
                else
                {
                    Console.Write("not-found, ");
                }

                Console.Write((string)keys[k] + ":" + ((OpFinderWordInfo)testList[keys[k]]).p);
                Console.Write(",");
                Console.WriteLine(miscInfo);

                OpinionFinderList.opTrainingWordList.Add(keys[k], testList[keys[k]]);

                testList.Clear();

           }
            Console.ReadLine();
        }

        /// <summary>
        /// This method is the core controller, then  runs different rules and compiles the polarities of words. Start from here to step through the code.
        /// </summary>
        /// <param name="newlyDiscoveredWords"></param>
        /// <param name="correctList"></param>
        /// <param name="incorrectList"></param>
        private static void Run(out Hashtable newlyDiscoveredWords, out ArrayList correctList, out ArrayList incorrectList)
        {
            SynsetClassifier synClassifier = new SynsetClassifier();
            ArrayList wordListForSecondPass = new ArrayList();
            ArrayList wordListForSecondPass2 = new ArrayList();

            determineSynsetPolarity d = synClassifier.determineSynsetPolarity;

            //Apply Rule0 and Rule1
            ComputePolarity(synClassifier, wordListForSecondPass, OpinionFinderList.opTrainingWordList.Keys, d);
            List<SimpleWord> list = new List<SimpleWord>();
            foreach (string words in wordListForSecondPass)
            {
                OpFinderWordInfo opwordInfo = (OpFinderWordInfo)OpinionFinderList.opTrainingWordList[words];
                SimpleWord wInfo = new SimpleWord(opwordInfo.word, Util.ConvertFromOpFinderPOS(opwordInfo.pos), opwordInfo.p);
                list.Add(wInfo);
            }

            //Apply Rule2
            d = synClassifier.determinSynsetPolarityByMinimalSet;
            ComputePolarity(synClassifier, wordListForSecondPass2, wordListForSecondPass, d);

            //Apply Hyponym Rule
            synClassifier.AssignPolarityAddHyponyms();

            //Now, that we applied all the rules to deduce polarity of synsets, lets
            //deduce the polarities of new words using those synsets and their polarities.
            newlyDiscoveredWords = new Hashtable();
            synClassifier.ComputePolarityOfWords(newlyDiscoveredWords);
            int count = newlyDiscoveredWords.Count;
           
            //Now we use this list, to deduce polarities for more words.
            //You may want to comment this if you want to test accuracy of rules. Any incorrectly
            //deduced words in this list may propagate the errors.
            Hashtable bootStrapList = new Hashtable(newlyDiscoveredWords);
            Dictionary<string, WordInfo> dict = new Dictionary<string, WordInfo>();
            int step = 1;
            
            //Now bootstrap, refere to previous note.
            //This while loop pretty much applies the set of rules we applied above.
            //The loop continues until no more words/synsets can be deduced.
            while (true)
            {
                wordListForSecondPass = new ArrayList();
                wordListForSecondPass2 = new ArrayList();
                d = synClassifier.determineSynsetPolarity;
        
                //Rule0 and Rule1
                ComputePolarity(synClassifier, wordListForSecondPass, bootStrapList.Keys, d, bootStrapList);
                List<SimpleWord> list2 = new List<SimpleWord>();
                foreach (string word in wordListForSecondPass)
                {
                    WordInfo wInfo = (WordInfo)bootStrapList[word];
                    list2.Add(new SimpleWord(wInfo.word, wInfo.synInfo.lemma.PartOfSpeech, wInfo.p));
                }

                list.AddRange(list2);
                d = synClassifier.determinSynsetPolarityByMinimalSet;
                //Rule 2
                ComputePolarity(synClassifier, wordListForSecondPass2, wordListForSecondPass, d, bootStrapList);

                //HyponymRule
                synClassifier.AssignPolarityAddHyponyms();

                //Compute the polority of the next set of new words, using the additional synsets 
                //identified in this pass.
                Hashtable nextList = synClassifier.ComputePolarityOfWords(newlyDiscoveredWords);

                //quit, if this pass did not give us any new words.
                if (nextList.Count == 0)
                {
                    // newlyDiscoveredWords = bootStrapList;
                    break;
                }
                else
                    bootStrapList = nextList;

                step++;
                
            }

            //Compute the results of the run. This method has a print statement that has been commented out. Check it out if you want that info.
            ComputePolarityRecall(newlyDiscoveredWords, OpinionFinderList.opTestWordList, out correctList, out incorrectList);

        }

        #region "Helper methods to compute polarity"
        
        /// <summary>
        /// Computes the recall statistics for the run
        /// </summary>
        /// <param name="newWords"></param>
        /// <param name="opFinderTestingList"></param>
        /// <param name="correctList"></param>
        /// <param name="incorrectList"></param>
        public static void ComputePolarityRecall(Hashtable newWords, Hashtable opFinderTestingList,
                                                    out ArrayList correctList, out ArrayList incorrectList)
        {

            int positiveCount = 0;
            int negativeCount = 0;
            int notFoundCount = 0;
            correctList = new ArrayList();
            incorrectList = new ArrayList();

            foreach (WordInfo wInfo in newWords.Values)
            {
                OpFinderWordInfo opWordInfo = (OpFinderWordInfo)opFinderTestingList[wInfo.word + ":" + wInfo.synInfo.lemma.PartOfSpeech];
                if (opWordInfo == null)
                    opWordInfo = (OpFinderWordInfo)opFinderTestingList[wInfo.word + ":" + OpFinderPOS.AnyPos];


                if (opWordInfo == null)
                {
                    //Console.WriteLine("NotFound:" + wInfo.word);
                    notFoundCount++;
                }
                else
                {
                    if (opWordInfo.p == wInfo.p)
                    {
                        correctList.Add(wInfo);
                        positiveCount++;
                    }
                    else
                    {
                        incorrectList.Add(wInfo);
                        negativeCount++;
                        //Console.WriteLine( "error" + ":" + opWordInfo.word + ":" + wInfo.p + "root:"+ wInfo.synInfo.rootWord + ","+  opWordInfo.p + "," + wInfo.synInfo.polarity );
                    }
                }

            }
        }

        /// <summary>
        /// Call the relavant rules for each word in the list.
        /// </summary>
        /// <param name="synClassifier"></param>
        /// <param name="wordListForSecondPass"></param>
        /// <param name="keys"></param>
        /// <param name="d"></param>
        private static void ComputePolarity(SynsetClassifier synClassifier,
                                            ArrayList wordListForSecondPass,
                                            ICollection keys,
                                            determineSynsetPolarity d)
        {
            foreach (string key in keys)
            {
                //try
                //{
                OpFinderWordInfo opWInfo = (OpFinderWordInfo)OpinionFinderList.opTrainingWordList[key];

                //mapping opfinder polarity to ours
                if (opWInfo.p != Polarity.Both) //We will handle the other case later
                {
                    if (opWInfo.pos == OpFinderPOS.AnyPos)
                    {
                        //Console.Write("Addressing AnyPos");
                        if (!d(opWInfo.word, PartOfSpeech.Noun, opWInfo.p))
                            wordListForSecondPass.Add(key);

                        if (!d(opWInfo.word, PartOfSpeech.Adjective, opWInfo.p))
                            wordListForSecondPass.Add(key);

                        if (!d(opWInfo.word, PartOfSpeech.Adverb, opWInfo.p))
                            wordListForSecondPass.Add(key);

                        if (!d(opWInfo.word, PartOfSpeech.Verb, opWInfo.p))
                            wordListForSecondPass.Add(key);
                    }
                    else
                    {
                        if (!d(opWInfo.word, Util.ConvertFromOpFinderPOS(opWInfo.pos), opWInfo.p))
                            wordListForSecondPass.Add(key);
                    }
                }
                //}
                //catch (Exception ex)
                //{
                //Console.WriteLine("Exception: " + ex.Message);
                //}
            }
        }

        //This is the method that bootstraps
        private static void ComputePolarity(SynsetClassifier synClassifier,
                                          ArrayList wordListForSecondPass,
                                          ICollection keys,
                                          determineSynsetPolarity d,
                                          Hashtable htNewWords)
        {
            foreach (string key in keys)
            {
                //try
                //{
                WordInfo wInfo = (WordInfo)htNewWords[key];
                //mapping opfinder polarity to ours
                if (!d(wInfo.word, wInfo.synInfo.lemma.PartOfSpeech, wInfo.p))
                    //if (!d(key, PartOfSpeech.Adjective,Polarity.Postive ))
                    wordListForSecondPass.Add(key);
                //}
                //catch (Exception ex)
                //{
                //Console.WriteLine("Exception: " + ex.Message);
                //}
            }
        }
        #endregion

    }


    class GraphNodeWordSynset
    {
        public IConcept value;
        public String type;
        public int flag = 0;
        public GraphNodeWordSynset(IConcept v, String t,int f)
        {
            value = v;
            type = t;
            flag = f;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    class Program
    {
        public static UndirectedGraph<GraphNodeWordSynset, Edge<GraphNodeWordSynset>> undirectedWNGraph = new UndirectedGraph<GraphNodeWordSynset, Edge<GraphNodeWordSynset>>(false);
        public static Dictionary<string, GraphNodeWordSynset> dictWords = new Dictionary<string, GraphNodeWordSynset>();
        public static List<ILemma> listWords = new List<ILemma>();
        public static List<ISynset> listSynsets = new List<ISynset>();
        public static Dictionary<int, GraphNodeWordSynset> dictSynsets = new Dictionary<int, GraphNodeWordSynset>();
        public static ILexicon lexDict = LexiconDict.getLexiconDict();
        public static ConnectedComponentsAlgorithm<GraphNodeWordSynset, Edge<GraphNodeWordSynset>> m_connectedComponents = null;
        public static ILexicon lexiconWN = null;
        public static ArrayList keys;
        public static Dictionary<int, List<OpFinderWordInfo>> componentLists_labeled = new Dictionary<int, List<OpFinderWordInfo>>();
        public static Dictionary<int, PartOfSpeech> componentPos = new Dictionary<int, PartOfSpeech>();
        
        public static int numberOfWordsTested = 1;

        public static Dictionary<int, List<ILemma>> componentLists_unlabeledWords = new Dictionary<int, List<ILemma>>();
        public static Dictionary<int, List<ISynset>> componentLists_unlabeledSynsets = new Dictionary<int, List<ISynset>>();

        public static Dictionary<string, ILemma> dictKnownWords = new Dictionary<string, ILemma>();


        static void Main(string[] args)
        {
             //Create Graph using Wordnet
            //Program.Test();
            Program.CreateGraph();
            Console.WriteLine("number of words:" + dictWords.Count);
            Console.WriteLine("number of synsets:" + dictSynsets.Count);
            Console.WriteLine("number of nodes in Graph:" + undirectedWNGraph.VertexCount);
            Console.WriteLine("number of edges in Graph:" + undirectedWNGraph.EdgeCount);

            // Compute Connected Components
            m_connectedComponents = new ConnectedComponentsAlgorithm<GraphNodeWordSynset, Edge<GraphNodeWordSynset>>(undirectedWNGraph);
            m_connectedComponents.Compute();
            int m_iComponentCount = m_connectedComponents.ComponentCount;
            Console.WriteLine("number of components:" + m_iComponentCount);
            Program.getComponents();
            
           // Get Component 0
           int components_num = 0;
           List<OpFinderWordInfo> listKnownWords;
           componentLists_labeled.TryGetValue(components_num, out listKnownWords);
           List<ILemma> listUnknownWords;
           componentLists_unlabeledWords.TryGetValue(components_num, out listUnknownWords);
           List<ISynset> listSynsets;
           componentLists_unlabeledSynsets.TryGetValue(components_num, out listSynsets);

            //OpFinderWordInfo ww = new OpFinderWordInfo();
            //ww.type = " "; ww.word = "test"; ww.pos = OpFinderPOS.Noun; ww.p = Polarity.Undefined;
            //listKnownWords.Add(ww);
            // Deduction

           // Deduction
           SynsetClassifier synClassifier;
           Hashtable newlyDiscoveredWords;
           //initialize
           Hashtable WC = new Hashtable();
           Hashtable WordPolarity = new Hashtable();
           Hashtable W1 = new Hashtable();
           foreach (OpFinderWordInfo w in listKnownWords)
           {
               SimpleWord simple = new SimpleWord(w.word, Util.ConvertFromOpFinderPOS(w.pos), w.p);
               WordPolarity.Add(w.word + ":" + w.pos, simple);
               WC.Add(w.word + ":" + w.pos, simple);
           }
           foreach (ILemma w in listUnknownWords)
           {
               SimpleWord simple = new SimpleWord(w.Text, w.PartOfSpeech, Polarity.Undefined);
               W1.Add(w.Text + ":" + w.PartOfSpeech, simple);
               WC.Add(w.Text + ":" + w.PartOfSpeech, simple);
           }
            //SynsetClassifier synClassifier;
            //Hashtable newlyDiscoveredWords;
           RunAnalysis.Deduction(listKnownWords, listUnknownWords, listSynsets, out newlyDiscoveredWords, out synClassifier);
           Console.Out.WriteLine("#New Words Found:" + newlyDiscoveredWords.Count);
           Console.Out.WriteLine("#New Synsets Found:" + synClassifier.ht.Count);

           //update initialize 
           int num = 0;
           foreach (WordInfo w in newlyDiscoveredWords.Values)
           {
               SimpleWord simple = new SimpleWord(w.word, w.synInfo.lemma.PartOfSpeech, w.p);
               if (WC[w.word + ":" + w.synInfo.lemma.PartOfSpeech] == null)
               {
                   num++;
                   WC.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech,simple);
                   WordPolarity.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, simple);
               }
               else 
               {
                   if (WordPolarity[w.word + ":" + w.synInfo.lemma.PartOfSpeech] == null)
                   {
                       WordPolarity.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, simple);
                       W1.Remove(w.word + ":" + w.synInfo.lemma.PartOfSpeech);
                       WC.Remove(w.word + ":" + w.synInfo.lemma.PartOfSpeech);
                       WC.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, simple);
                   }
                   //WC.Remove(w.word + ":" + w.synInfo.lemma.PartOfSpeech);
                   //WC.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, simple);
               }
           }
           Hashtable SynsetPolarity = synClassifier.ht;//Syninfo
           Hashtable SYN = new Hashtable();//ISynset
           Hashtable S = new Hashtable();//ISynset
           int count = 0;
           foreach (ISynset syn in listSynsets)
           {
               SYN.Add(syn.SynsetId, syn);
               if (SynsetPolarity[syn.SynsetId] == null)
                   S.Add(syn.SynsetId, syn);
           }
           foreach (SynsetInfo syn in SynsetPolarity.Values)
           {
               if (SYN[syn.lemma.SynsetId] == null)
               {
                   ISynset newsyn = syn.lemma.Synset;
                   SYN.Add(newsyn.SynsetId, newsyn);
                   //S.Add(newsyn.SynsetId, newsyn);
                   count++;
               }
           }
           polarityInference polInference = new polarityInference(WC, WordPolarity, W1, SYN, SynsetPolarity, S, synClassifier);
           //polInference.computeAverageNumberNewSentimentalWords(S);
           polInference.mainfunction();
           Console.ReadLine();
            //Console.Out.WriteLine("#New Words Found:" + newlyDiscoveredWords.Count);
            //Console.Out.WriteLine("#New Synsets Found:" + synClassifier.ht.Count);
            ////updatelists_afterdeduction(listKnownWords,  listUnknownWords, listSynsets, out newlyDiscoveredWords, synClassifier);
            
            //Console.ReadLine();
        }
        /*public static void updatelists_afterdeduction( List<OpFinderWordInfo> listKnownWords, 
                                                       List<ILemma> listUnknownWords, 
                                                       List<ISynset> listSynsets,
                                                      out Hashtable newlyDiscoveredWords,
                                                      SynsetClassifier synClassifier)
        {
            foreach (OpFinderWordInfo wInfo in listKnownWords)
            {
                
                //listUnknownWords.Add();
            }
        }*/
        public static void getComponents()
        {
            for (int i = 0; i < m_connectedComponents.ComponentCount; i++)
            {
                List<OpFinderWordInfo> listl = new List<OpFinderWordInfo>();
                componentLists_labeled.Add(i, listl);
                List<ILemma> listuw = new List<ILemma>();
                componentLists_unlabeledWords.Add(i, listuw);
                List<ISynset> listus = new List<ISynset>();
                componentLists_unlabeledSynsets.Add(i, listus);
            }
            
            /*
            for (int k = 0; k < numberOfWordsTested; k++)
            {
                OpFinderWordInfo wInfo = (OpFinderWordInfo)OpinionFinderList.opTrainingWordList[keys[k]];
                IList<ILemma> list = lexiconWN.FindSenses(wInfo.word, Util.ConvertFromOpFinderPOS(wInfo.pos));
                int iCurrentComponent = -1;


                foreach (ILemma lem in list)
                {
                    try
                    {
                        if (!lem.PartOfSpeech.Equals(Util.ConvertFromOpFinderPOS(wInfo.pos)))
                        {
                            Console.Out.WriteLine("Part of Speech Miss Match");
                            continue;
                        }

                        GraphNodeWordSynset node1;
                        dictWords.TryGetValue(lem.Text + ":" + lem.PartOfSpeech, out node1);
                        iCurrentComponent = m_connectedComponents.Components[node1];
                        List<OpFinderWordInfo> listwi;
                        bool success = componentLists_labeled.TryGetValue(iCurrentComponent, out listwi);
                        listwi.Add(wInfo);
                        if (!componentPos.ContainsKey(iCurrentComponent))
                        {
                            componentPos.Add(iCurrentComponent, Util.ConvertFromOpFinderPOS(wInfo.pos));
                        }
                        else
                        {
                            PartOfSpeech posnow;
                            componentPos.TryGetValue(iCurrentComponent, out posnow);
                            if (!posnow.Equals(lem.PartOfSpeech))
                            {
                                Console.Out.WriteLine("Part of Speech Miss Match");
                            }
                        }
                        break;
                    }catch{}  
                }
            }*/
            foreach (ILemma lma in listWords)
            {
                int iCurrentComponent;
                GraphNodeWordSynset node1;
                bool success;
                if (OpinionFinderList.opTrainingWordList.Contains(lma.Text + ":" + lma.PartOfSpeech))
                {
                    OpFinderWordInfo wInfo = (OpFinderWordInfo)OpinionFinderList.opTrainingWordList[lma.Text + ":" + lma.PartOfSpeech];

                    dictWords.TryGetValue(lma.Text + ":" + lma.PartOfSpeech, out node1);
                     iCurrentComponent = m_connectedComponents.Components[node1];
                    List<OpFinderWordInfo> listwi;
                     success = componentLists_labeled.TryGetValue(iCurrentComponent, out listwi);
                    listwi.Add(wInfo);
                    if (!componentPos.ContainsKey(iCurrentComponent))
                    {
                        componentPos.Add(iCurrentComponent, Util.ConvertFromOpFinderPOS(wInfo.pos));
                    }
                    else
                    {
                        PartOfSpeech posnow;
                        componentPos.TryGetValue(iCurrentComponent, out posnow);
                        if (!posnow.Equals(lma.PartOfSpeech))
                        {
                            Console.Out.WriteLine("Part of Speech Miss Match");
                        }
                    }

                    continue;
                }

                dictWords.TryGetValue(lma.Text + ":" + lma.PartOfSpeech, out node1);
                iCurrentComponent = m_connectedComponents.Components[node1];
                List<ILemma> listword;
                 success = componentLists_unlabeledWords.TryGetValue(iCurrentComponent, out listword);
                listword.Add(lma);
                if (!componentPos.ContainsKey(iCurrentComponent))
                {
                    componentPos.Add(iCurrentComponent, lma.PartOfSpeech);
                }
                else
                {
                    PartOfSpeech posnow;
                    componentPos.TryGetValue(iCurrentComponent, out posnow);
                    if (!posnow.Equals(lma.PartOfSpeech))
                    {
                        Console.Out.WriteLine("Part of Speech Miss Match");
                    }
                }



            }

            foreach (ISynset syna in listSynsets)
            {
                GraphNodeWordSynset syn;
                dictSynsets.TryGetValue(syna.SynsetId, out syn);
                int iCurrentComponent = m_connectedComponents.Components[syn];
                List<ISynset> listsyn;
                bool success = componentLists_unlabeledSynsets.TryGetValue(iCurrentComponent, out listsyn);
                listsyn.Add(syna);

                if (!componentPos.ContainsKey(iCurrentComponent))
                {
                    componentPos.Add(iCurrentComponent, syna.PartOfSpeech);
                }
                else
                {
                    PartOfSpeech posnow;
                    componentPos.TryGetValue(iCurrentComponent, out posnow);
                    if (!posnow.Equals(syna.PartOfSpeech))
                    {
                        Console.Out.WriteLine("Part of Speech Miss Match");
                    }
                }


            }
            //foreach(IConcept concept in dictWords)
        }

        // search for a word
        public static void search(ILemma lemma)
        {
            GraphNodeWordSynset node1;
            dictWords.TryGetValue(lemma.Text + ":" + lemma.PartOfSpeech, out node1);

            PartOfSpeech pos = lemma.PartOfSpeech;
            IList<ILemma> senses = lexDict.FindSenses(lemma.Text, lemma.PartOfSpeech);

            foreach (ILemma s1 in senses)
            {
                if (!s1.PartOfSpeech.Equals(pos))
                {
                    Console.WriteLine("Part of Speech Miss Match!");
                    continue;
                }
                ISynset synset = s1.Synset;
                GraphNodeWordSynset node2;
                Edge<GraphNodeWordSynset> edge;
                if (dictSynsets.ContainsKey(synset.SynsetId))
                {
                    dictSynsets.TryGetValue(synset.SynsetId, out node2);
                    if (node2.flag == 1)
                    {
                        edge = new Edge<GraphNodeWordSynset>(node1, node2);
                        undirectedWNGraph.AddEdge(edge);
                    }
                    continue;
                }

                node2 = new GraphNodeWordSynset(synset, "synset",1);
                dictSynsets.Add(synset.SynsetId, node2);
                listSynsets.Add(synset);

                undirectedWNGraph.AddVertex(node2);
                edge = new Edge<GraphNodeWordSynset>(node1, node2);
                undirectedWNGraph.AddEdge(edge);

                // Search all the words in the synset
                foreach (ILemma ss1 in synset.Lemmas)
                {
                    if (!ss1.PartOfSpeech.Equals(pos))
                    {
                        Console.WriteLine("Part of Speech Miss Match!");
                        continue;
                    }
                    GraphNodeWordSynset node3;
                    if (!dictWords.ContainsKey(ss1.Text + ":" + ss1.PartOfSpeech))
                    {
                        node3 = new GraphNodeWordSynset(ss1, "word",1);
                        //Console.WriteLine(ss1.Text+":"+ss1.PartOfSpeech);
                        dictWords.Add(ss1.Text + ":" + ss1.PartOfSpeech, node3);
                        listWords.Add(ss1);

                        undirectedWNGraph.AddVertex(node3);
                        edge = new Edge<GraphNodeWordSynset>(node3, node2);
                        undirectedWNGraph.AddEdge(edge);

                        search(ss1);
                        node3.flag = 2;
                    }
                    else
                    {
                        dictWords.TryGetValue(ss1.Text + ":" + ss1.PartOfSpeech, out node3);
                        if (node3.flag == 1)
                        {
                            edge = new Edge<GraphNodeWordSynset>(node3, node2);
                            undirectedWNGraph.AddEdge(edge);
                        }
                    }
                }
                
            }
        }


        // Creat a small graph
        public static void CreateGraphV2()
        {

        }

        public static void CreateGraph()
        {
            // Load WordNet data
            string ctProxemDataFile = "C:\\Program Files\\Proxem\\Antelope\\data\\Proxem.Lexicon.dat";
            lexiconWN = new Lexicon();
            lexiconWN.LoadDataFromFile(ctProxemDataFile, null);

            // Load Training Data
            OpinionFinderList.LoadOpinionFinderWordList(Properties.OpinionFinderFileAll, Properties.OpinionFinderTestFile, true);

            // Test a subset of the training data
            if (numberOfWordsTested > OpinionFinderList.opTrainingWordList.Count)
                numberOfWordsTested = OpinionFinderList.opTrainingWordList.Count;
            int count = numberOfWordsTested;
            keys = new ArrayList();
            keys.AddRange(OpinionFinderList.opTrainingWordList.Keys);


            Hashtable testList = new Hashtable();

            for (int k = 0; k < count; k++)
            {
                // get one word from training set
                OpFinderWordInfo wInfo = (OpFinderWordInfo)OpinionFinderList.opTrainingWordList[keys[k]];
                PartOfSpeech pos=Util.ConvertFromOpFinderPOS(wInfo.pos);
                IList<ILemma> list = lexiconWN.FindSenses(wInfo.word, pos);

                //Loop for all senses from the training word
                foreach (ILemma lemma in list)
                {
                    if (!lemma.PartOfSpeech.Equals(pos))
                    {
                        Console.WriteLine("Part of Speech Miss Match!"); continue;
                    }
                    ISynset syn = lemma.Synset;
                    GraphNodeWordSynset node1;
                    if (!dictSynsets.ContainsKey(syn.SynsetId))
                    {
                        node1 = new GraphNodeWordSynset(lemma.Synset,"synset",1);
                        dictSynsets.Add(lemma.SynsetId, node1);
                        listSynsets.Add(lemma.Synset);
                        undirectedWNGraph.AddVertex(node1);
                    }
                    else
                    {
                        dictSynsets.TryGetValue(syn.SynsetId,out node1);
                        if (node1.flag == 2) continue;
                    }
                    // get all the words in the synset
                    foreach (ILemma ss1 in lemma.Synset.Lemmas)
                    {
                        if (!ss1.PartOfSpeech.Equals(pos))
                        {
                            Console.WriteLine("Part of Speech Miss Match!"); continue;
                        }
                        Edge<GraphNodeWordSynset> edge;
                        GraphNodeWordSynset node2;
                        if (!dictWords.ContainsKey(ss1.Text + ":" + ss1.PartOfSpeech))
                        {
                            node2 = new GraphNodeWordSynset(ss1, "word",1);
                            //Console.WriteLine(ss1.Text + ":" + ss1.PartOfSpeech);
                            dictWords.Add(ss1.Text + ":" + ss1.PartOfSpeech, node2);
                            listWords.Add(ss1);
                            undirectedWNGraph.AddVertex(node2);

                            edge = new Edge<GraphNodeWordSynset>(node2, node1);
                            undirectedWNGraph.AddEdge(edge);
                            search(ss1);
                            node2.flag = 2;
                        }
                        else
                        {
                            GraphNodeWordSynset lem;
                            dictWords.TryGetValue(ss1.Text + ":" + ss1.PartOfSpeech, out lem);
                            if (lem.flag == 1)
                            {
                                edge = new Edge<GraphNodeWordSynset>(lem, node1);
                                undirectedWNGraph.AddEdge(edge);
                            }
                        }
                        
                    }
                   
 



                }
            }




        }















    }
}
