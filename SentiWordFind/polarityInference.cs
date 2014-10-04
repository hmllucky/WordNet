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
    /// <summary>
    /// Class that initialize the heap.
    /// </summary>
    public class polarityInference
    {
        private Hashtable synWmax = new Hashtable();//LIST
       
       
        Hashtable WC = new Hashtable();//全部
        Hashtable WordPolarity = new Hashtable();//已知
        Hashtable W1 = new Hashtable();
        Hashtable SYN = new Hashtable();//全部
        Hashtable S = new Hashtable();//未知POLARITY
        Hashtable SynsetPolarity = new Hashtable();//已知POLARITY
        //Hashtable SynPol = new Hashtable();
        Hashtable Httotalfrequence = new Hashtable();
        SynsetClassifier synClassifier = new SynsetClassifier();

        public polarityInference(Hashtable _WC, Hashtable _WordPolarity,Hashtable _W1, Hashtable _SYN, Hashtable _SynsetPolarity,Hashtable _S, SynsetClassifier _synClassifier)
        {
            WC = _WC;
            WordPolarity = _WordPolarity;
            W1 = _W1;

            SYN = _SYN;
            SynsetPolarity = _SynsetPolarity;
            S = _S;
            //Httotalfrequence = _Httotalfrequence;
            synClassifier = _synClassifier;
            Httotalfrequence = null;
            
        }
        public static ILexicon lexDict = LexiconDict.getLexiconDict();
        
        public class SynNode
        {
            public double E;
            public int SynId;
        }
        ArrayList SynNodes = new ArrayList();


        //// <summary>
        //// Intital Dataset
        //// </summary>
        public void Initialize()
        {
            int start = 0;
            foreach (WordInfo w in WC.Values)
            {
                if (w.p == Polarity.Undefined) W1.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, w);
                else if ((start % 5) == 0)
                {
                    w.p = Polarity.Undefined;
                    W1.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, w);
                }
                else WordPolarity.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, w);
                start++;
            }
            foreach (WordInfo w in W1.Values)
            {
                WC.Remove(w.word + ":" + w.synInfo.lemma.PartOfSpeech);
                WC.Add(w.word + ":" + w.synInfo.lemma.PartOfSpeech, w);
            }
            SynsetClassifier SynsetClassifier = new SynsetClassifier();
            foreach(WordInfo w in WC.Values)
            {
                IList<ILemma> senses = lexDict.FindSenses(w.word, w.synInfo.lemma.PartOfSpeech);
                //float frequence = SynsetClassifier.calculateTotalFrequency(senses);
                float frequence = synClassifier.calculateTotalFrequency(senses);
                Httotalfrequence.Add(w.word +":"+w.synInfo .lemma.PartOfSpeech ,frequence);
                
                foreach(ILemma sense in senses)
                {
                    SynsetInfo synset= new SynsetInfo(sense,w.p, w.word, null, "T0");
                    if (SYN[sense.Synset.SynsetId]==null)
                    {
                        SYN.Add(sense.Synset.SynsetId, synset);
                        if (synset.polarity == Polarity.Undefined)
                            S.Add(sense.Synset.SynsetId, synset);
                        else
                            SynsetPolarity.Add(sense.Synset.SynsetId, synset);
                    }
                }
            }
            //mainfunction(S, SynsetPolarity);
            mainfunction();
            //WordInfo w=WordPolarity.
            //foreach (WordInfo w in WordPolarity.Values){
            //    Console.Write(w.synInfo.lemma.Text+"\n");
            //    Console.Write(w.synInfo.lemma.Synset+"\n");
            //    Console.Write(w.word + "\n");
            //    Console.Write(w.synInfo.lemma.Synset.Lemmas.ToString() + "\n");
            //    Console.Write(w.synInfo.lemma.Synset.Lemmas.Count + "\n");
            //     }

        }

        //// <summary>
        //// Method calculated the per(wj,positive),per(wj,negtive),per(wj,neutral)
        //// </summary>
        private void perpos(string word, PartOfSpeech pos, int synID, out double rightcoef, out double leftcoef)
        {
            double num = 0;
            leftcoef = 0;
            rightcoef = 0;
            //if (WC[word + ":" + pos] == null)
            //{
            //    SimpleWord wtemp = new SimpleWord(word, pos, Polarity.Undefined);
            //    WC.Add(word + ":" + pos, wtemp);
            //    W1.Add(word + ":" + pos, wtemp);
            //    Console.WriteLine("Add world\n");
            //}

            SimpleWord w = (SimpleWord)WC[word + ":" + pos];
            ///////////////check//////////////
            IList<ILemma> senses = lexDict.FindSenses(w.word, w.pos);
            
            float totalFrequency = synClassifier.calculateTotalFrequency(word,pos);

            foreach(ILemma sense in senses)
            {
                if (sense.SynsetId == synID)
                {
                    if ((totalFrequency - (double)sense.Frequency) > 0.1)
                        leftcoef = (double)sense.Frequency / totalFrequency;
                    else
                        leftcoef = 1;
                }
                //else if (SYN[sense.SynsetId] != null)
                //else if (SYN[sense.SynsetId] == null)
                //{
                //    ISynset synset = sense.Synset;
                //    SYN.Add(sense.SynsetId, synset);
                //    S.Add(sense.SynsetId, synset);
                //    Console.WriteLine("Add synset\n");
                //}
                else
                {
                    if (SynsetPolarity[sense.SynsetId] != null)
                    {
                        SynsetInfo syn = (SynsetInfo)SynsetPolarity[sense.SynsetId];
                        if (syn.polarity == Polarity.Postive)
                            num += (double)sense.Frequency / totalFrequency;

                    }
                    else 
                    {
                       if (w.p == Polarity.Postive)
                           num += (double)sense.Frequency / totalFrequency;
                       else if (w.p == Polarity.Undefined)
                           num += (double)(sense.Frequency) * ((double)1 / (double)3) / totalFrequency;
                    }
                }
            }
            rightcoef = num;
        }
        private void perneg(string word, PartOfSpeech pos, int synID, out double rightcoef, out double leftcoef)
        {
            double num = 0;
            leftcoef = 0;
            rightcoef = 0;
            //if (WC[word + ":" + pos] == null)
            //{
            //    SimpleWord wtemp = new SimpleWord(word, pos, Polarity.Undefined);
            //    WC.Add(word + ":" + pos, wtemp);
            //    W1.Add(word + ":" + pos, wtemp);
            //}

            SimpleWord w = (SimpleWord)WC[word + ":" + pos];
            ///////////////check//////////////
            IList<ILemma> senses = lexDict.FindSenses(w.word, w.pos);

            float totalFrequency = synClassifier.calculateTotalFrequency(word,pos);

            foreach (ILemma sense in senses)
            {
                if (sense.SynsetId == synID)
                {
                    if ((totalFrequency - (double)sense.Frequency) > 0.1)
                        leftcoef = (double)sense.Frequency / totalFrequency;
                    else
                        leftcoef = 1;
                }
                //else if (SYN[sense.SynsetId] != null)
                //else if (SYN[sense.SynsetId] == null)
                //{
                //    ISynset synset=sense.Synset;
                //    SYN.Add(sense.SynsetId,synset);
                //    S.Add(sense.SynsetId, synset);
                //    Console.WriteLine("Add synset\n");
                //}
                else 
                {
                    if (SynsetPolarity[sense.SynsetId] != null)
                    {
                        SynsetInfo syn = (SynsetInfo)SynsetPolarity[sense.SynsetId];
                        if (syn.polarity == Polarity.Negative)
                            num += (double)sense.Frequency / totalFrequency;

                    }
                    else
                    {
                        if (w.p == Polarity.Negative)
                            num += (double)sense.Frequency / totalFrequency;
                        else if (w.p == Polarity.Undefined)
                            num += (double)(sense.Frequency) * ((double)1 / (double)3) / totalFrequency;
                    }
                }
            }
            rightcoef = num;
        }
        private void perneutral(string word, PartOfSpeech pos, int synID, out double rightcoef, out double leftcoef)
        {
            double num = 0;
            leftcoef = 0;
            rightcoef = 0;
            //if (WC[word + ":" + pos] == null)
            //{
            //    SimpleWord wtemp = new SimpleWord(word, pos, Polarity.Undefined);
            //    WC.Add(word + ":" + pos, wtemp);
            //    W1.Add(word + ":" + pos, wtemp);
            //}

            SimpleWord w = (SimpleWord)WC[word + ":" + pos];
            ///////////////check//////////////
            IList<ILemma> senses = lexDict.FindSenses(w.word, w.pos);

            float totalFrequency = synClassifier.calculateTotalFrequency(senses);

            foreach (ILemma sense in senses)
            {
                if (sense.SynsetId == synID)
                {
                    if ((totalFrequency - (double)sense.Frequency) > 0.1)
                        leftcoef = (double)sense.Frequency / totalFrequency;
                    else
                        leftcoef = 1;
                }
                //else if (SYN[sense.SynsetId] != null)
                //else if (SYN[sense.SynsetId] == null)
                //{
                //    ISynset synset = sense.Synset;
                //    SYN.Add(sense.SynsetId, synset);
                //    S.Add(sense.SynsetId, synset);
                //    Console.WriteLine("Add synset\n");
                //}
                else
                {
                    if (SynsetPolarity[sense.SynsetId] != null)
                    {
                        SynsetInfo syn = (SynsetInfo)SynsetPolarity[sense.SynsetId];
                        if (syn.polarity == Polarity.Neutral)
                            num += (double)sense.Frequency / totalFrequency;

                    }
                    else
                    {
                        if (w.p == Polarity.Neutral)
                            num += (double)sense.Frequency / totalFrequency;
                        else if (w.p == Polarity.Undefined)
                            num += (double)(sense.Frequency) * ((double)1 / (double)3) / totalFrequency;
                    }
                }
            }
            rightcoef = num;
        }


        //// <summary>
        //// Method calculated the expected number
        //// </summary>
        //private void computeAverageNumberNewSentimentalWords(Hashtable t_S, Hashtable SynsetPolarity) //后一个没有用SynsetPolarity
        public void computeAverageNumberNewSentimentalWords(Hashtable t_S) //后一个没有用SynsetPolarity
        {
            if (SynNodes!=null) SynNodes.Clear();
            foreach (ISynset synset in t_S.Values)
            {
                int Npos, Nneg, Nneu;
                Npos = 0; Nneg = 0; Nneu = 0;
                
                Hashtable Wmaxpos = new Hashtable();
                Hashtable Wmaxneg = new Hashtable();
                Hashtable Wmaxneutral = new Hashtable();

                //if (synset.SynsetId == 15205532)
                //    Npos = 0;

                foreach (ILemma sense in synset.Lemma.Synset.Lemmas)
                {
                    //if (sense.Text.Contains(' ')) 
                    //    Console.WriteLine("WRONG" + sense.Text+"\n");
                    //if (WC[sense.Text + ":" + synset.Lemma.PartOfSpeech] == null)
                    //{
                    //    WC.Add(sense.Text + ":" + synset.Lemma.PartOfSpeech,new SimpleWord(sense.Text,synset.Lemma.PartOfSpeech,Polarity.Undefined));
                    //    W1.Add(sense.Text + ":" + synset.Lemma.PartOfSpeech,new SimpleWord(sense.Text,synset.Lemma.PartOfSpeech,Polarity.Undefined));
                    //    Console.WriteLine("Add word2\n");
                    //}
                    if (WordPolarity[sense.Text + ":" + synset.Lemma.PartOfSpeech] == null)
                    {
                        SimpleWord w = (SimpleWord)WC[sense.Text + ":" + synset.Lemma.PartOfSpeech];//可以W1
                        if (w == null) continue;
                        w.p = ComputePolarityOfWord(w, synset.Lemma.SynsetId, Polarity.Postive);
                        if (w.p != Polarity.Undefined)
                        {
                            Npos++;
                            if (Wmaxpos[w.word + ":" + w.pos] == null)
                                Wmaxpos.Add(w.word + ":" + w.pos, w);
                            w.p = Polarity.Undefined;
                        }
                        w.p = ComputePolarityOfWord(w, synset.Lemma.SynsetId, Polarity.Negative);
                        if (w.p != Polarity.Undefined)
                        {
                            Nneg++;
                            if (Wmaxneg[w.word + ":" + w.pos] == null)
                                Wmaxneg.Add(w.word + ":" + w.pos, w);
                            w.p = Polarity.Undefined;
                        }
                        w.p = ComputePolarityOfWord(w, synset.Lemma.SynsetId, Polarity.Neutral);
                        if (w.p != Polarity.Undefined)
                        {
                            Nneu++;
                            if (Wmaxneutral[w.word + ":" + w.pos] == null)
                                Wmaxneutral.Add(w.word + ":" + w.pos, w);
                            w.p = Polarity.Undefined;
                        }
                    }
                }
                synWmax.Add(synset.Lemma.SynsetId.ToString() + ":" + "Postive", Wmaxpos);
                synWmax.Add(synset.Lemma.SynsetId.ToString() + ":" + "Negative", Wmaxneg);
                synWmax.Add(synset.Lemma.SynsetId.ToString() + ":" + "Neutral", Wmaxneutral);

                int SynsetSize = synset.Lemma.Synset.Lemmas.Count;
                
                double leftcoef, rightcoef;
                double leftpos = 1;
                double rightpos = 0;
                double leftneg = 1;
                double rightneg = 0;
                double leftneutral = 1;
                double rightneutral = 0;

                foreach (ILemma sense in synset.Lemma.Synset.Lemmas)
                {
                    //double m;
                    //if (synset.SynsetId == 7846)
                    //    m = 7846;
                    string word = sense.Text;
                    PartOfSpeech pos = synset.Lemma.PartOfSpeech;
                    perpos(word, pos, synset.Lemma.SynsetId, out rightcoef, out leftcoef);
                    leftpos -= leftcoef / (double)SynsetSize;
                    rightpos += rightcoef / (double)SynsetSize;

                    perneg(word, pos, synset.Lemma.SynsetId, out rightcoef, out leftcoef);
                    leftneg -= leftcoef / (double)SynsetSize;
                    rightneg += rightcoef / (double)SynsetSize;

                    perneutral(word, pos, synset.Lemma.SynsetId, out rightcoef, out leftcoef);
                    leftneutral -= leftcoef / (double)SynsetSize;
                    rightneutral += rightcoef / (double)SynsetSize;
                }
                //double t1 = rightpos / leftpos;
                //double t2 = rightneg / leftneg;
                //double t3 = rightneutral / leftneutral;
                double t1 = leftpos > 0 ? rightpos / leftpos : ((double)1 / (double)3);
                double t2 = leftneg > 0 ? rightneg / leftneg : ((double)1 / (double)3);
                double t3 = leftneutral > 0 ? rightneutral / leftneutral : ((double)1 / (double)3);

                if ((t1 + t2 + t3)>1)
                   Console.WriteLine(t1+t2+t3);

                SynNode s = new SynNode();
                s.E = t1 * (double)Npos + t2 * (double)Nneg + t3 * (double)Nneu;
                s.SynId = synset.Lemma.SynsetId;
                //if (s.SynId == 15205532)
                //    s.E = s.E - 1;
                if (s.E > 0)
                {
                    SynNodes.Add(s);
                }
            }
        }
        //// <summary>
        //// Method calculated the polarity of word
        //// </summary>
        public Polarity ComputePolarityOfWord(SimpleWord wInfo, int SynsetId,Polarity p)
        {
            
            IList<ILemma> senses = lexDict.FindSenses(wInfo.word, wInfo.pos);

            if (senses == null || senses.Count == 0) return Polarity.Undefined;

            float totalFrequency = synClassifier.calculateTotalFrequency(senses);

            if (senses.Count == 1)
            {
                if (senses[0].SynsetId == SynsetId)
                {
                    return p;
                }
            }
            if ((senses.Count == 2 && senses[0].Frequency == senses[1].Frequency))
            {
                //if ((SYN[senses[0].SynsetId] != null) && (SYN[senses[1].SynsetId] != null))
                {
                    Polarity p1 = Polarity.Undefined; Polarity p2 = Polarity.Undefined;
                    if (SynsetPolarity[senses[0].SynsetId] != null)
                    {
                        SynsetInfo syn= (SynsetInfo)SynsetPolarity[senses[0].SynsetId];
                        p1 = syn.polarity;
                    }
                    if (SynsetPolarity[senses[1].SynsetId] != null)
                    {
                        SynsetInfo syn = (SynsetInfo)SynsetPolarity[senses[1].SynsetId];
                        p2 = syn.polarity;
                    }

                    if (senses[0].SynsetId == SynsetId)
                        p1 = p;
                    else if (senses[1].SynsetId == SynsetId)
                        p2 = p;
                    if (p1 == p2 && p1 != Polarity.Undefined)
                        return p;
                }
            }
            else
            {
                int index = 0;
                if (index < senses.Count)
                {
                    float freq = (senses[index].Frequency == 0) ? (float)0.1 : senses[index].Frequency;
                    if (freq > (0.5 * totalFrequency))
                    {
                        if (senses[index].SynsetId == SynsetId)
                        {
                           return p;
                        }
                    }
                }
            }

            int posPolarity = 0;
            int negPolarity = 0;
            int neutralPolarity = 0;

            foreach (ILemma sense in senses)
            {
                if ((SynsetInfo)SynsetPolarity[sense.SynsetId] != null)
                {
                    SynsetInfo synInfo = ((SynsetInfo)SynsetPolarity[sense.SynsetId]);

                    if (synInfo.polarity == Polarity.Postive)
                    {
                        posPolarity = posPolarity + sense.Frequency;
                    }
                    else if (synInfo.polarity == Polarity.Negative)
                    {
                        negPolarity = negPolarity + sense.Frequency;
                    }
                    else if (synInfo.polarity == Polarity.Neutral)
                    {
                        neutralPolarity = neutralPolarity + sense.Frequency;
                    }
                }
                else if (sense.SynsetId == SynsetId)
                {
                    if (p == Polarity.Postive) posPolarity = posPolarity + sense.Frequency;
                    else if (p == Polarity.Negative) negPolarity = negPolarity + sense.Frequency;
                    else if (p == Polarity.Neutral) neutralPolarity = neutralPolarity + sense.Frequency;
                }
            }

            if (posPolarity > (0.5 * totalFrequency))
            {
                //wInfo.SetDerivedCondition("W2");
                return Polarity.Postive;
            }
            else if (negPolarity > (0.5 * totalFrequency))
            {
                //wInfo.SetDerivedCondition("W2");
                return Polarity.Negative;
            }
            else if (neutralPolarity > (0.5 * totalFrequency))
            {
                //wInfo.SetDerivedCondition("W2");
                return Polarity.Neutral;
            }

            return Polarity.Undefined;
        }
        //// <summary>
        //// Method calculated the polarity of word by majoritydefinition
        //// </summary>
        //public Polarity majoritydefinition(WordInfo w)
        //{
        //    IList<ILemma> senses = lexDict.FindSenses(w.word, w.synInfo.lemma.PartOfSpeech);

        //    if (senses == null || senses.Count == 0) return Polarity.Undefined;//没有SENSE的，无法DEFINE
        //    int posPolarity = 0;
        //    int negPolarity = 0;
        //    int neutralPolarity = 0;
        //    foreach (ILemma sense in senses)
        //    {
        //        if ((SynsetInfo)SYN[sense.SynsetId] != null)
        //        {
        //            SynsetInfo synInfo = ((SynsetInfo)SYN[sense.SynsetId]);

        //            if (synInfo.polarity == Polarity.Postive)
        //            {
        //                posPolarity = posPolarity + sense.Frequency;
        //            }
        //            else if (synInfo.polarity == Polarity.Negative)
        //            {
        //                negPolarity = negPolarity + sense.Frequency;
        //            }
        //            else if (synInfo.polarity == Polarity.Neutral)
        //            {
        //                neutralPolarity = neutralPolarity + sense.Frequency;
        //            }
        //        }
        //    }
        //    //int max = Math.Max(posPolarity, negPolarity);
        //    //int max2 = Math.Max(max,neutralPolarity);
        //    int max=negPolarity;
        //    if (posPolarity > negPolarity)
        //        max = posPolarity;
        //    if (neutralPolarity > max)
        //        max = neutralPolarity;
        //    if (max == posPolarity) return Polarity.Postive;
        //    else if (max == negPolarity) return Polarity.Negative;
        //    else return Polarity.Neutral;
        //}
        public Polarity majoritydefinition(SimpleWord wInfo)
        {
            int SynsetId = -1;
            //IList<ILemma> senses = lexDict.FindSenses(wInfo.word, wInfo.synInfo.lemma.PartOfSpeech);
            IList<ILemma> senses = lexDict.FindSenses(wInfo.word, wInfo.pos);

            if (senses == null || senses.Count == 0) return Polarity.Undefined;

            float totalFrequency = synClassifier.calculateTotalFrequency(senses);
            if (senses.Count == 1)
            {
                if (SynsetPolarity[senses[0].SynsetId] != null)
                {
                    //wInfo.AddSynContrib((SynsetInfo)SynsetPolarity[senses[0].SynsetId]);
                    //wInfo.SetDerivedCondition("Only One Synset");
                    SynsetId = senses[0].SynsetId;
                    return ((SynsetInfo)SynsetPolarity[senses[0].SynsetId]).polarity;
                }
            }
            if ((senses.Count == 2 && senses[0].Frequency == senses[1].Frequency))
            {


                Polarity p1 = Polarity.Undefined;
                Polarity p2 = Polarity.Undefined;

                if (SynsetPolarity[senses[1].SynsetId] != null)
                    p1 = ((SynsetInfo)SynsetPolarity[senses[1].SynsetId]).polarity;
                else if (SynsetPolarity[senses[0].SynsetId] != null)
                    p2 = ((SynsetInfo)SynsetPolarity[senses[0].SynsetId]).polarity;
                //else
                //  return Polarity.Undefined;

                if (p1 == p2 && p1 != Polarity.Undefined)
                {


                    //wInfo.AddSynContrib((SynsetInfo)SynsetPolarity[senses[0].SynsetId]);
                    //wInfo.AddSynContrib((SynsetInfo)SynsetPolarity[senses[1].SynsetId]);
                    //wInfo.SetDerivedCondition("2 Synsets with same frequency and polarity");

                    SynsetId = senses[0].SynsetId;
                    return p1;
                }
            }
            else//senses.Count>2
            {
                int index = 0;
                if (index < senses.Count)
                {
                    float freq = (senses[index].Frequency == 0) ? (float)0.1 : senses[index].Frequency;
                    if (freq > (0.5 * totalFrequency))
                    {
                        //wInfo.SetDerivedCondition("First Synset Dominant by Frequency Count");
                        if (SynsetPolarity[senses[index].SynsetId] != null)
                        {
                            //wInfo.AddSynContrib((SynsetInfo)SynsetPolarity[senses[0].SynsetId]);
                            SynsetId = senses[index].SynsetId;
                            return ((SynsetInfo)SynsetPolarity[senses[index].SynsetId]).polarity;
                        }
                    }
                }
            }

            int posPolarity = 0;
            int negPolarity = 0;
            int neutralPolarity = 0;

            foreach (ILemma sense in senses)
            {
                if (SynsetPolarity[sense.SynsetId] != null)
                {
                    SynsetInfo synInfo = ((SynsetInfo)SynsetPolarity[sense.SynsetId]);

                    if (synInfo.polarity == Polarity.Postive)
                    {
                        posPolarity = posPolarity + sense.Frequency;
                    }
                    else if (synInfo.polarity == Polarity.Negative)
                    {
                        negPolarity = negPolarity + sense.Frequency;
                    }
                    else if (synInfo.polarity == Polarity.Neutral)
                    {
                        neutralPolarity = neutralPolarity + sense.Frequency;
                    }
                }
            }

            if (posPolarity > (0.5 * totalFrequency))
            {
                //wInfo.SetDerivedCondition("W2");
                return Polarity.Postive;
            }
            else if (negPolarity > (0.5 * totalFrequency))
            {
                //wInfo.SetDerivedCondition("W2");
                return Polarity.Negative;
            }
            else if (neutralPolarity > (0.5 * totalFrequency))
            {
                //wInfo.SetDerivedCondition("W2");
                return Polarity.Neutral;
            }

            return Polarity.Undefined;
            //return Polarity.Postive;

        }
        


        //// <summary>
        //// Method initial the heap structure
        //// </summary>
        //private void mainfunction(Hashtable S, Hashtable SynsetPolarity)
        public void mainfunction()
        {
            //computeAverageNumberNewSentimentalWords(S,SynsetPolarity);
            computeAverageNumberNewSentimentalWords(S);

            Heap heap = new Heap(SynNodes);
            heap.constructHeap();
            runHeap(heap);
        }

        //// <summary>
        //// Method initial run the heap
        //// </summary>
        public void runHeap(Heap heap)
        {
            Hashtable Wmax = new Hashtable();
            Hashtable Wprime = new Hashtable();
            if ((heap.heapsize() == 0) || (WordPolarity.Count == WC.Count)) return;
            else
            {
                SynNode s = (polarityInference.SynNode)heap.A[0];
                string str = "";
                /////<summary>
                ///// squence 2 & 3
                /////</summary> 
                //ISynset smax = (ISynset)SYN[s.SynId];
                ISynset smax = (ISynset)S[s.SynId];
                Console.WriteLine("expected number"+s.E+"\n");
                heap.Dequeue();
                /////<summary>
                ///// squence 4
                /////</summary> 
                Console.WriteLine("please decide the polarity for the following synset; (-1: neg, 0: neutral, 1: pos)");
                //Console.WriteLine(smax.lemma.Text);
                Console.WriteLine(smax.Lemma.Synset);        
                str=Console.ReadLine();
                int pos;
                pos = System.Convert.ToInt32(str);
                while ((pos != -1) && (pos != 1) && (pos != 0))
                {
                    Console.WriteLine("please reenter the polarity for the following synset; (-1: neg, 0: neutral, 1: pos)");
                    Console.WriteLine(smax.Lemma.Text);
                    str = Console.ReadLine();
                    pos = System.Convert.ToInt32(str);
                }
                //SynsetInfo snewmax= new SynsetInfo(smax.Lemma, Polarity.Undefined, null, null, null);
                string root="A";
                SynsetInfo snewmax= new SynsetInfo(smax.Lemma, Polarity.Undefined, root, null, null);
                //SynsetInfo snewmax = new SynsetInfo(smax.Lemma, Polarity.Undefined, smax.Synset.Lemmas[0].ToString(), null, null);
                if (pos == -1)
                {
                    snewmax = new SynsetInfo(smax.Lemma, Polarity.Negative, root, null, null);//不是DEDUCTION rootword 没有
                    Wmax = (Hashtable)synWmax[smax.Lemma.SynsetId + ":" + "Negative"];
                }
                else if (pos == 0)
                {
                    snewmax = new SynsetInfo(smax.Lemma, Polarity.Neutral, root, null, null);
                    Wmax = (Hashtable)synWmax[smax.Lemma.SynsetId + ":" + "Neutral"];
                }
                else if (pos == 1)
                {
                    snewmax = new SynsetInfo(smax.Lemma, Polarity.Postive, root, null, null);
                    Wmax = (Hashtable)synWmax[smax.Lemma.SynsetId + ":" + "Postive"];
                }
                /////<summary>
                ///// squence 5,7
                /////</summary> 
                if(Wmax.Count==0)
                {
                    runHeap(heap);
                }

                /////<summary>
                ///// squence 7 & 8
                /////</summary> 
                foreach (SimpleWord w in Wmax.Values)
                {
                    if (WordPolarity[w.word + ":" + w.pos] == null)
                    {
                        WordPolarity.Add(w.word + ":" + w.pos, w);
                    }
                }
                List<OpFinderWordInfo> listKnownWords = new List<OpFinderWordInfo>();
                foreach (SimpleWord wmax in Wmax.Values)
                {
                    IList<ILemma> senses = lexDict.FindSenses(wmax.word, wmax.pos);
                    foreach (ILemma sense in senses)
                    {
                        foreach (ILemma sense2 in sense.Synset.Lemmas)
                        {
                            //SimpleWord w = (SimpleWord)WordPolarity[sense2.Text + ":" + sense.PartOfSpeech];
                            //if (w != null)
                            if (WordPolarity[sense2.Text + ":" + sense.PartOfSpeech] != null)
                            {
                                SimpleWord w = (SimpleWord)WordPolarity[sense2.Text + ":" + sense.PartOfSpeech];
                                if (Wprime[sense2.Text + ":" + sense.PartOfSpeech] == null)
                                {
                                    Wprime.Add(w.word + ":" + sense.PartOfSpeech, w);
                                    OpFinderWordInfo wwInfo = new OpFinderWordInfo();
                                    wwInfo.word = w.word;
                                    wwInfo.p = w.p; wwInfo.pos = (OpFinderPOS)w.pos;
                                    wwInfo.type = " ";
                                    OpFinderWordInfo wInfo = new OpFinderWordInfo(wwInfo, wwInfo.pos);
                                    listKnownWords.Add(wInfo);
                                    //OpinionFinderList.opTrainingWordList.Add(wInfo);
                                }
                            }
                        }
                    }
                }
                /////<summary>
                ///// squence 9
                /////</summary> 
                Hashtable newlyDiscoveredWords = new Hashtable();
                Hashtable W1 = new Hashtable();
                Hashtable SynPol = new Hashtable();//applyInference(out W1,out SynPol)
                //List<OpFinderWordInfo> listKnownWords = null;

                //Hashtable newlyDiscoveredWords = new Hashtable();
                //SynsetClassifier new_synClassifier=new SynsetClassifier();
                //RunAnalysis.Deduction(listKnownWords, null, null, out newlyDiscoveredWords, out synClassifier);
                synClassifier.ht.Add( snewmax.lemma.SynsetId,snewmax );
                List<ILemma> listUnknownWords=new List<ILemma>();
                List<ISynset> listSynsets=new List<ISynset>();
                RunAnalysis.Deduction(listKnownWords, listUnknownWords, listSynsets, out newlyDiscoveredWords, out synClassifier);//应该把smax加进去
                SynPol = synClassifier.ht;
                foreach (WordInfo w in newlyDiscoveredWords.Values)//可能超出范围
                {
                    SimpleWord simple = new SimpleWord(w.word, w.synInfo.lemma.PartOfSpeech, w.p);
                    W1.Add(simple.word + ":" + simple.pos, simple);
                }
                //SynPol = synClassifier.ht - SynsetPolarity;
                //SynsetPolarity = synClassifier.ht;
                
                //foreach (WordInfo w in newlyDiscoveredWords.Values)
                //{
                //    if (WordPolarity[w.word + ":" + w.synInfo.lemma.PartOfSpeech] == null)
                //    {
                //        SimpleWord simple=new SimpleWord(w.word, w.synInfo.lemma.PartOfSpeech, w.p);
                //        W1.Add(simple.word + simple.pos, simple);
                //    }
                //}
                /////<summary>
                ///// squence 10
                /////</summary>
                foreach (SimpleWord w in W1.Values)
                {
                    if (WordPolarity[w.word + ":" + w.pos] == null)
                        WordPolarity.Add(w.word + ":" + w.pos, w);
                }
                /////<summary>
                ///// squence 11,12
                /////</summary>
                foreach (SynsetInfo syn in SynPol.Values)
                {
                    if (SynsetPolarity[syn.lemma.SynsetId] == null)
                    {
                        SynsetPolarity.Add(syn.lemma.SynsetId, syn);
                        S.Remove(syn.lemma.SynsetId);
                    }
                }
                if (SynsetPolarity[smax.Lemma.SynsetId] == null)
                {
                    SynsetPolarity.Add(smax.Lemma.SynsetId, snewmax);
                    S.Remove(smax.Lemma.SynsetId);
                }
                /////<summary>
                ///// squence 13
                /////</summary>
                if (S.Count == 0)
                {
                    foreach (SimpleWord w in WC.Values)
                    {
                        if (WordPolarity[w.word + ":" + w.pos] == null)//可以优化 只针对polarity不知道的
                        {
                            w.p = majoritydefinition(w);
                            if(w.p!=Polarity.Undefined)
                               WordPolarity.Add(w.word + ":" + w.pos, w);
                        }
                    }
                    //return;
                }
                /////<summary>
                ///// squence 14
                /////</summary>
                Hashtable Snew = new Hashtable();
                foreach (ISynset syn in S.Values)
                {
                    foreach (ILemma sense in syn.Lemma.Synset.Lemmas)
                    {
                         if (Wmax[sense.Text + ":" + syn.Lemma.PartOfSpeech] != null)
                         {
                             if (Snew[syn.Lemma.SynsetId] == null)
                             {
                                 Snew.Add(syn.Lemma.SynsetId, syn);
                                 continue;
                             }
                         }
                         else if (W1[sense.Text + ":" + syn.Lemma.PartOfSpeech] != null)
                         {
                             if (Snew[syn.Lemma.SynsetId] == null)
                             {
                                 Snew.Add(syn.Lemma.SynsetId, syn);
                                 continue;
                             }
                         }
                    }
                }
                /////<summary>
                ///// squence 15
                /////</summary>
                synWmax.Clear();//这个需要更新
                SynNodes.Clear();//这个也需要
                //computeAverageNumberNewSentimentalWords(Snew, SynsetPolarity);
                computeAverageNumberNewSentimentalWords(Snew);
                /////<summary>
                ///// squence 16
                /////</summary>
                heap.removeFromHeap(SynPol);
                /////<summary>
                ///// squence 17
                /////</summary>
                heap.updateHeap(SynNodes);
                /////<summary>
                ///// squence 18
                /////</summary>
                runHeap(heap);
            } 
        }
        //// <summary>
        //// Method initial run the heap
        //// </summary>
        //public bool shareOneSynset(int synID, WordInfo w, Hashtable SynsetListWmax)
        //{
        //    List<int> SynsetList = (List<int>)SynsetListWmax[w];
        //    foreach (int value in SynsetList)
        //    {
        //        if (value == synID)
        //            return true;
        //    }
        //    return false;
        //}
        public bool shareOneSynset(List<int> SynsetList, List<int> SynsetList2)
        {
            int i = 0; int j = 0;
            while ((i < SynsetList.Count) && (j < SynsetList2.Count))
            {
                if (SynsetList[i] == SynsetList2[j])
                    return true;
                else if (SynsetList[i] < SynsetList2[j])
                    i++;
                else j++;
            }
            return false;
        }

    }

    /// <summary>
    /// Class of heap and correpondging method
    /// </summary>
    public class Heap
    {
        public ArrayList A = null;
        public System.Collections.Hashtable HashSyn = new System.Collections.Hashtable();

        public Heap() { A =new ArrayList(); }
        public Heap(ArrayList SynNodes) { A = SynNodes; }
        public int heapsize()
        {
            return A.Count;
        }

        //// <summary>
        ////methods of constructheap and corresponding synHash
        ////</summary>
        public void constructHeap()
        {
            BuildMaxHeap();
            for (int i = 0; i < A.Count;i++ )
            {
                HashSyn.Add(((polarityInference.SynNode)A[i]).SynId, i);
            }
        }
        //// <summary>
        ////methods of maxheapify
        //// </summary>
        public void MaxHeapify(int i)
        {
            int left = 2 * i+1;
            int right = 2 * i+2;
            int largest = i;
            if (left >= A.Count) return;
            if (left < A.Count)
            {
                if (((polarityInference.SynNode)A[left]).E > ((polarityInference.SynNode)A[i]).E)
                    largest = left;
            }
            if (right < A.Count)
            {
                if (((polarityInference.SynNode)A[right]).E > ((polarityInference.SynNode)A[largest]).E) 
                    largest = right;
            }
            if (largest != i)
            {
                polarityInference.SynNode temp = (polarityInference.SynNode)A[i];
                A[i] = A[largest];
                A[largest] = temp;
                MaxHeapify(largest);
            }
        }
        public void MaxHeapify_modified(int i)
        {
            int left = 2 * i + 1;
            int right = 2 * i + 2;
            int largest = i;
            if (left >= A.Count) return;
            if (left < A.Count)
            {
                if (((polarityInference.SynNode)A[left]).E > ((polarityInference.SynNode)A[i]).E)
                    largest = left;
            }
            if (right < A.Count)
            {
                if (((polarityInference.SynNode)A[right]).E > ((polarityInference.SynNode)A[largest]).E)
                    largest = right;
            }
            if (largest != i)
            {
                polarityInference.SynNode temp = (polarityInference.SynNode)A[i];

                HashSyn.Remove(((polarityInference.SynNode)A[i]).SynId);
                HashSyn.Remove(((polarityInference.SynNode)A[largest]).SynId);

                A[i] = A[largest];
                A[largest] = temp;
                
                HashSyn.Add(((polarityInference.SynNode)A[i]).SynId, i);
                HashSyn.Add(((polarityInference.SynNode)A[largest]).SynId,largest);
                MaxHeapify_modified(largest);
            }
        }

        //// <summary>
        ////methods of build max heap
        //// </summary>
        public void BuildMaxHeap()
        {
            int start = (int)Math.Floor((double)(A.Count) / 2)-1;
            for (int i = start; i >=0; i--)
            {
                MaxHeapify(i);
            }
        }
        //// <summary>
        ////methods of build max heap
        //// </summary>
        public void Dequeue()
        {
            HashSyn.Remove(((polarityInference.SynNode)A[0]).SynId);
            A[0] = A[A.Count - 1];
            A.RemoveAt(A.Count-1);
            MaxHeapify_modified(0);
        }
        //// <summary>
        ////methods of remove set of synsets from the heap H
        //// </summary>
        public void removeFromHeap(Hashtable S)
        {
            if ((S.Count == 0) || (A.Count == 0)) return;
            foreach (SynsetInfo syn in S.Values)
            {
                int ID;
                if (HashSyn[syn.lemma.SynsetId] != null)
                {
                    ID =(int) HashSyn[syn.lemma.SynsetId];
                    A[ID] = A[A.Count - 1];
                    A.RemoveAt(A.Count - 1);
                    updatePositionInHeapFor(ID);
                }   
            }
            
        }
        //// <summary>
        ////methods of update the positioln in the heap H
        //// </summary>
        public void updatePositionInHeapFor(int ID)
        {
            polarityInference.SynNode v = (polarityInference.SynNode)A[ID];
            int leafstart = (int)Math.Floor((double)A.Count / 2);
            if (v == null) return;
            if (ID > 0)
            {
                if (((polarityInference.SynNode)A[parent(ID)]).E < ((polarityInference.SynNode)A[ID]).E)
                {
                    while ((ID > 0) && (((polarityInference.SynNode)A[parent(ID)]).E < ((polarityInference.SynNode)A[ID]).E))
                    {
                        polarityInference.SynNode w = (polarityInference.SynNode)A[parent(ID)];
                        HashSyn.Remove(((polarityInference.SynNode)A[ID]).SynId);
                        HashSyn.Remove(((polarityInference.SynNode)A[parent(ID)]).SynId);
                        A[parent(ID)] = A[ID];
                        A[ID] = w;
                        HashSyn.Add(((polarityInference.SynNode)A[ID]).SynId, ID);
                        HashSyn.Add(((polarityInference.SynNode)A[parent(ID)]).SynId, parent(ID));
                        ID = parent(ID);
                    }
                }
                else
                {
                    while ((ID < leafstart) && ((((polarityInference.SynNode)A[ID]).E < ((polarityInference.SynNode)A[2 * ID + 1]).E) || (((polarityInference.SynNode)A[ID]).E < ((polarityInference.SynNode)A[2 * ID + 2]).E)))
                    {
                        polarityInference.SynNode w = (polarityInference.SynNode)A[ID]; int child = -1;
                        if (((polarityInference.SynNode)A[2 * ID + 1]).E >= ((polarityInference.SynNode)A[2 * ID + 2]).E)
                        {
                            child = 2 * ID + 1;
                        }
                        else
                        {//A[2*ID+1].E<A[2*ID+2].E
                            child = 2 * ID + 2;
                        }
                        HashSyn.Remove(((polarityInference.SynNode)A[ID]).SynId);
                        HashSyn.Remove(((polarityInference.SynNode)A[child]).SynId);
                        A[ID] = A[child];
                        A[child] = w;
                        HashSyn.Add(((polarityInference.SynNode)A[ID]).SynId, ID);
                        HashSyn.Add(((polarityInference.SynNode)A[child]).SynId, child);
                        ID = child;
                    }
                }
            }
            else
            {
                while ((ID < leafstart) && ((((polarityInference.SynNode)A[ID]).E < ((polarityInference.SynNode)A[2 * ID + 1]).E) || (((polarityInference.SynNode)A[ID]).E < ((polarityInference.SynNode)A[2 * ID + 2]).E)))
                {
                     polarityInference.SynNode w = (polarityInference.SynNode)A[ID]; int child = -1;
                     if (((polarityInference.SynNode)A[2 * ID + 1]).E >= ((polarityInference.SynNode)A[2 * ID + 2]).E)
                     {
                         child = 2 * ID + 1;
                     }
                     else
                     {//A[2*ID+1].E<A[2*ID+2].E
                            child = 2 * ID + 2;
                     }
                     HashSyn.Remove(((polarityInference.SynNode)A[ID]).SynId);
                     HashSyn.Remove(((polarityInference.SynNode)A[child]).SynId);
                     A[ID] = A[child];
                     A[child] = w;
                     HashSyn.Add(((polarityInference.SynNode)A[ID]).SynId, ID);
                     HashSyn.Add(((polarityInference.SynNode)A[child]).SynId, child);
                     ID = child;
                }
            }
        }
            //HashSyn.Clear();
            //for (int i = 0; i < A.Count; i++)
            //{
            //    HashSyn.Add(A[i].SynId, i);
            //}
        
        private int parent(int index)
        {
            int position;
            if (index == 0) return (-1);
            position = (int)Math.Floor(((double)(index-1)) / 2);
            return (position);
        }
        //// <summary>
        ////methods of update the heap H
        //// </summary>
        public void updateHeap(ArrayList SynNodes)
        {
            if ((SynNodes.Count == 0) || (A.Count == 0)) return; 
            foreach (polarityInference.SynNode syn in SynNodes)
            {
                int ID;
                if (HashSyn[syn.SynId] != null)
                {
                    ID = (int)HashSyn[syn.SynId];
                    ((polarityInference.SynNode)A[ID]).E = syn.E;
                    updatePositionInHeapFor(ID);
                }
            }
        }
    }

    /// <summary>
    /// Class of heap and correpondging method
    /// </summary>
    
}