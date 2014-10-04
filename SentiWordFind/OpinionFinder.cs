using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;

namespace SentiWordFind
{
    
/*    public enum opType
    {
        weaksubj,
        strongsubj
    }
*/

    public enum OpFinderPOS
    {
        AnyPos = -1,
        Noun = 0,
        Verb = 1,
        Adjective = 2,
        Adverb = 3
    }

  
    public class OpFinderWordInfo
    {
        public string word;
        public Polarity p;
        public String type;
        public OpFinderPOS pos;

        public OpFinderWordInfo() { }

        public OpFinderWordInfo(OpFinderWordInfo wInfo, OpFinderPOS pos)
        {
            this.word = wInfo.word;
            this.p = wInfo.p;
            this.type = wInfo.type;
            this.pos = pos;
        }
    }

    /// <summary>
    /// Class load the opfinder list based on values in the Properties class.
    /// </summary>
    public class OpinionFinderList
    {
        
        public static System.Collections.Hashtable opTrainingWordList = new System.Collections.Hashtable();
        public static System.Collections.Hashtable opTestWordList = new System.Collections.Hashtable();

        public static void LoadOpinionFinderWordList(string fileName, string testFileName, bool Opfinder)
        {
            if (Opfinder)
            {
                opTrainingWordList = LoadOpinionFinderWordList(fileName);
                opTestWordList = LoadOpinionFinderWordList(testFileName);
            }       
        }


        //Call this method before using the hashtable(ht).
        public static Hashtable LoadOpinionFinderWordList(string fileName)
        {

            Hashtable htLocal = new Hashtable();
            StreamReader reader = new StreamReader(fileName);

            while (true)
            {
                string line = reader.ReadLine();
                if (string.IsNullOrEmpty(line)) break;

                string[] nameValuePairs = line.Split(new char[] { ' ' });

                OpFinderWordInfo wInfo = new OpFinderWordInfo();
                foreach (string nvPair in nameValuePairs)
                {
                    string[] nv = nvPair.Split(new char[] { '=' });

                    if (nv[0].Equals("type"))
                    {
                        wInfo.type = nv[1];
                    }
                    else if (nv[0].Equals("word1"))
                    {
                        wInfo.word = nv[1];
                    }
                    else if (nv[0].Equals("priorpolarity"))
                    {
                        if (nv[1].Equals("positive"))
                        {
                            wInfo.p = Polarity.Postive;
                        }
                        else if (nv[1].Equals("negative") || nv[1].Equals("weakneg"))
                        {
                            wInfo.p = Polarity.Negative;
                        }
                        else if (nv[1].Equals("neutral"))
                        {
                            wInfo.p = Polarity.Neutral;
                        }
                        else if (nv[1].Equals("both"))
                        {
                            wInfo.p = Polarity.Both;
                        }
                        else
                            throw new Exception("Unknown polarity " + nv[1]);

                    }
                    else if (nv[0].Equals("pos1"))
                    {
                        if (nv[1].Equals("adverb"))
                        {
                            wInfo.pos = OpFinderPOS.Adverb;
                        }
                        else if (nv[1].Equals("noun"))
                        {
                            wInfo.pos = OpFinderPOS.Noun;
                        }
                        else if (nv[1].Equals("adj"))
                        {
                            wInfo.pos = OpFinderPOS.Adjective;
                        }
                        else if (nv[1].Equals("verb"))
                        {
                            wInfo.pos = OpFinderPOS.Verb;
                        }
                        else if (nv[1].Equals("anypos"))
                        {
                            wInfo.pos = OpFinderPOS.AnyPos;
                        }
                        else
                        {
                            throw new Exception("Unknown POS " + nv[1]);
                        }

                    }


                }

                try
                {
                    OpFinderWordInfo w = (OpFinderWordInfo)htLocal[wInfo.word + ":" + wInfo.pos];
                    if (w != null)
                    {
                        if (w.p == wInfo.p)
                            continue;
                        else
                        {
                            w.p = Polarity.Both;
                            continue;
                        }
                    }

                    if (wInfo.pos != OpFinderPOS.AnyPos)
                    {
                        if (LexiconDict.getLexiconDict().FindSenses(wInfo.word, Util.ConvertFromOpFinderPOS(wInfo.pos)).Count > 0)
                            htLocal.Add(wInfo.word + ":" + wInfo.pos, wInfo);
                    }
                    else
                    {
                        if (LexiconDict.getLexiconDict().FindSenses(wInfo.word, Util.ConvertFromOpFinderPOS(OpFinderPOS.Adjective)).Count > 0)
                            htLocal.Add(wInfo.word + ":" + OpFinderPOS.Adjective, new OpFinderWordInfo(wInfo, OpFinderPOS.Adjective));

                        if (LexiconDict.getLexiconDict().FindSenses(wInfo.word, Util.ConvertFromOpFinderPOS(OpFinderPOS.Adverb)).Count > 0)
                            htLocal.Add(wInfo.word + ":" + OpFinderPOS.Adverb, new OpFinderWordInfo(wInfo, OpFinderPOS.Adverb));

                        if (LexiconDict.getLexiconDict().FindSenses(wInfo.word, Util.ConvertFromOpFinderPOS(OpFinderPOS.Noun)).Count > 0)
                            htLocal.Add(wInfo.word + ":" + OpFinderPOS.Noun, new OpFinderWordInfo(wInfo, OpFinderPOS.Noun));

                        if (LexiconDict.getLexiconDict().FindSenses(wInfo.word, Util.ConvertFromOpFinderPOS(OpFinderPOS.Verb)).Count > 0)
                            htLocal.Add(wInfo.word + ":" + OpFinderPOS.Verb, new OpFinderWordInfo(wInfo, OpFinderPOS.Verb));

                    }

                }
                catch (ArgumentException arg)
                {
                    //Console.WriteLine("OpFindError:" + arg.Message);
                }

            }

            reader.Close();

            return htLocal;
        }



    }




}
