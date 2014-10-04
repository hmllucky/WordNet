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
    /// Class holds information on newly identified synsets for which polarities could be assigned.
    /// </summary>
    public class SynsetInfo
    {
        public ILemma lemma = null;
        public Polarity polarity = Polarity.Undefined;
        public string rootWord = string.Empty;
        public string derivedCondition = string.Empty;
        public RelationType relTypeRootWord = RelationType.IsMember;
        public SynsetInfo rootSynset = null;
        

        public SynsetInfo(ILemma lemma, Polarity p, string rootWord, SynsetInfo s, string derivedCondition)
        {
            this.lemma = lemma;
            this.polarity = p;
            this.rootWord = rootWord.ToLower();
            this.derivedCondition = derivedCondition;
            rootSynset = s;
        }

        public SynsetInfo(ILemma lemma, Polarity p, string rootWord, string derivedCondition, SynsetInfo s, RelationType relType)
        {
            this.lemma = lemma;
            this.polarity = p;
            this.rootWord = rootWord.ToLower();
            this.derivedCondition = derivedCondition;
            relTypeRootWord = relType;
            rootSynset = s;
            
        }
    }

    /// <summary>
    /// Helper class
    /// </summary>
    public class SimpleWord
    {
        public string word;
        public PartOfSpeech pos;
        public Polarity p;

        public SimpleWord(string _word, PartOfSpeech _pos, Polarity _p)
        {
            word = _word;
            pos = _pos;
            p = _p;
        }
    }

    /// <summary>
    /// Class to store word information
    /// </summary>
    public class WordInfo
    {
        public string word;
        public SynsetInfo synInfo;
        public List<SynsetInfo> synContribList = new List<SynsetInfo>();
        public string derivedCondition = string.Empty;
        public Polarity p = Polarity.Undefined;
        public ILemma rootLemma;

        public WordInfo(string word, SynsetInfo synInfo)
        {
            this.word = word.ToLower();
            this.synInfo = synInfo;

            if (synInfo.lemma == null) return;
            IList<ILemma> senses = LexiconDict.getLexiconDict().FindSenses(word, synInfo.lemma.PartOfSpeech);

            foreach (ILemma sense in senses)
            {
                if (sense.SynsetId == synInfo.lemma.SynsetId)
                {
                    rootLemma = sense;
                    break;
                }
            }

        }

        public int distanceFromRootWord = 0;

        public WordInfo(string word, SynsetInfo synInfo, int distance, ILemma sense)
        {
            this.word = word.ToLower();
            this.synInfo = synInfo;
            distanceFromRootWord = distance;
            rootLemma = sense;
        }

        public void SetDerivedCondition(string derivedCondition)
        {
            this.derivedCondition = derivedCondition;
        }

        public void AddSynContrib(SynsetInfo pcontrib)
        {
            synContribList.Add(pcontrib);
        }


    }


    /// <summary>
    /// Helper class for tracing/logging 
    /// </summary>
    public class SenseFrequencyInfo
    {
        public string word;
        public PartOfSpeech pos;
        public float totalFrequency;
        public bool areAllSynsetsBelowFrequency;
       
        public SenseFrequencyInfo(string _word, PartOfSpeech _pos, float totalFrequency, bool areAllSynsetsBelowFrequency)
        {
            this.word = _word;
            this.pos = _pos;
            this.totalFrequency = totalFrequency;
            this.areAllSynsetsBelowFrequency = areAllSynsetsBelowFrequency;

        }
    }

    public class SynsetClassifier2
    {

    }


    /// <summary>
    /// Class that does all the work of identifying synset polarities using the rules.
    /// </summary>
    public class SynsetClassifier
    {
        public System.Collections.Hashtable ht = new System.Collections.Hashtable();
        public Dictionary<string, SenseFrequencyInfo> senseFrequencyInfo = new Dictionary<string, SenseFrequencyInfo>();
        public static ILexicon lexDict = LexiconDict.getLexiconDict();
        public static Dictionary<int, ILemma> temp1;

        #region "Helper functions"
        //Note: The total frequencies are calculate and stored, so that we dont have to loop around all synsets of words while bootstrapping.
        public float calculateTotalFrequency(string word, PartOfSpeech pos)
        {

            float count = 0;

            if (!senseFrequencyInfo.ContainsKey(word + ":" + pos))
            {
                count = calculateTotalFrequency(lexDict.FindSenses(word, pos));
                senseFrequencyInfo.Add(word + ":" + pos, new SenseFrequencyInfo(word, pos, count, AreAllSynsetsBelowFrequency(lexDict.FindSenses(word, pos))));
                return count;
            }
            else
            {
                return senseFrequencyInfo[word + ":" + pos].totalFrequency;
            }
        }
        public float calculateTotalFrequency(IList<ILemma> senses)
        {

            float count = 0;
            foreach (ILemma lemma in senses)
            {
                count = lemma.Frequency > 0 ? (float)lemma.Frequency + count : count + (float)0.1;
            }

            //if ( senseFrequencyInfo.ContainsKey(

            return count;
        }
        private int calculateTotalFrequency2(IList<ILemma> senses)
        {
            int count = 0;
            int index = -1;
            foreach (ILemma lemma in senses)
            {
                index++;
                if (lexDict.FindSenses(lemma.Text, lemma.PartOfSpeech)[index].Lemma.Synset.Lemmas.Count == 1) continue;

                count = lemma.Frequency > 0 ? lemma.Frequency + count : count;
            }

            //if ( senseFrequencyInfo.ContainsKey(

            return count;
        }
        private bool AreAllSynsetsBelowFrequency(string word, PartOfSpeech pos)
        {

            if (!senseFrequencyInfo.ContainsKey(word + ":" + pos))
            {
                float count = calculateTotalFrequency(lexDict.FindSenses(word, pos));
                senseFrequencyInfo.Add(word + ":" + pos, new SenseFrequencyInfo(word, pos, count, AreAllSynsetsBelowFrequency(lexDict.FindSenses(word, pos))));
            }

            return senseFrequencyInfo[word + ":" + pos].areAllSynsetsBelowFrequency;

        }
        private bool AreAllSynsetsBelowFrequency(IList<ILemma> senses)
        {

            float totalFreq = calculateTotalFrequency(senses);

            foreach (ILemma sense in senses)
            {
                if (sense.Frequency > 0.5 * totalFreq)
                    return false;
            }

            return true;
        }
        public Polarity InversePolarity(Polarity p)
        {
            if (p == Polarity.Negative) return Polarity.Postive;
            if (p == Polarity.Postive) return Polarity.Negative;
            else return Polarity.Neutral;

        }

        #endregion

        #region "Computes the polarity of words using the synset polarities"
        public Polarity ComputePolarityOfWord(WordInfo wInfo, out int SynsetId)
        {
            SynsetId = -1;

            IList<ILemma> senses = lexDict.FindSenses(wInfo.word, wInfo.synInfo.lemma.PartOfSpeech);

            if (senses == null || senses.Count == 0) return Polarity.Undefined;

            float totalFrequency = calculateTotalFrequency(senses);
            if (senses.Count == 1)
            {
                if (ht[senses[0].SynsetId] != null)
                {
                    wInfo.AddSynContrib((SynsetInfo)ht[senses[0].SynsetId]);
                    wInfo.SetDerivedCondition("Only One Synset");
                    SynsetId = senses[0].SynsetId;
                    return ((SynsetInfo)ht[senses[0].SynsetId]).polarity;
                }
            }

            if ((senses.Count == 2 && senses[0].Frequency == senses[1].Frequency))
            {


                Polarity p1 = Polarity.Undefined;
                Polarity p2 = Polarity.Undefined;

                if (ht[senses[1].SynsetId] != null)
                    p1 = ((SynsetInfo)ht[senses[1].SynsetId]).polarity;
                else if (ht[senses[0].SynsetId] != null)
                    p2 = ((SynsetInfo)ht[senses[0].SynsetId]).polarity;
                //else
                //  return Polarity.Undefined;

                if (p1 == p2 && p1 != Polarity.Undefined)
                {


                    wInfo.AddSynContrib((SynsetInfo)ht[senses[0].SynsetId]);
                    wInfo.AddSynContrib((SynsetInfo)ht[senses[1].SynsetId]);
                    wInfo.SetDerivedCondition("2 Synsets with same frequency and polarity");


                    SynsetId = senses[0].SynsetId;
                    return p1;
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
                        wInfo.SetDerivedCondition("First Synset Dominant by Frequency Count");
                        if (ht[senses[index].SynsetId] != null)
                        {
                            wInfo.AddSynContrib((SynsetInfo)ht[senses[0].SynsetId]);
                            SynsetId = senses[index].SynsetId;
                            return ((SynsetInfo)ht[senses[index].SynsetId]).polarity;
                        }
                    }
                }
            }

            int posPolarity = 0;
            int negPolarity = 0;
            int neutralPolarity = 0;

            foreach (ILemma sense in senses)
            {
                if (ht[sense.SynsetId] != null)
                {
                    SynsetInfo synInfo = ((SynsetInfo)ht[sense.SynsetId]);

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
                wInfo.SetDerivedCondition("W2");
                return Polarity.Postive;
            }
            else if (negPolarity > (0.5 * totalFrequency))
            {
                wInfo.SetDerivedCondition("W2");
                return Polarity.Negative;
            }
            else if (neutralPolarity > (0.5 * totalFrequency))
            {
                wInfo.SetDerivedCondition("W2");
                return Polarity.Neutral;
            }

            return Polarity.Undefined;
            //return Polarity.Postive;

        }
        public Hashtable ComputePolarityOfWords(Hashtable htNewWords)
        {

            //Console.WriteLine("Here are the words");
            ArrayList newWords = new ArrayList();

            Hashtable htNewWords_2 = new Hashtable();
            foreach (SynsetInfo synInfo in ht.Values)
            {
                foreach (ILemma sense in synInfo.lemma.Synset.Lemmas)
                {
                    if (!synInfo.rootWord.Equals(sense.Text))
                    {
                        int SynsetId = -1;
                        WordInfo wInfo = new WordInfo(sense.Text, synInfo);
                        wInfo.p = ComputePolarityOfWord(wInfo, out SynsetId);
                        if (wInfo.p != Polarity.Undefined)
                        {
                            if (wInfo.synInfo.lemma.SynsetId != SynsetId && SynsetId > 0)
                                wInfo.synInfo = (SynsetInfo)ht[SynsetId];

                            if (htNewWords[sense.Text + ":" + wInfo.synInfo.lemma.PartOfSpeech] == null
                                && htNewWords[sense.Text.ToLower() + ":" + wInfo.synInfo.lemma.PartOfSpeech] == null)
                            {
                                htNewWords.Add(sense.Text + ":" + wInfo.synInfo.lemma.PartOfSpeech, wInfo);
                                //Console.WriteLine(sense.Text + ":" + wInfo.p + ":" + synInfo.rootWord + ":" + wInfo.derivedCondition + ":" + synInfo.derivedCondition);
                                htNewWords_2.Add(sense.Text + ":" + wInfo.synInfo.lemma.PartOfSpeech, wInfo);
                            }
                        }
                    }
                    else
                    {

                    }
                }
            }

            return htNewWords_2;

        }
        #endregion
        
        #region "Methods that map to the rules"

        /// <summary>
        /// Method uses Rule0 and Rule1 to deduce polarity of synsets
        /// </summary>
        /// <param name="knownWord"></param>
        /// <param name="pos"></param>
        /// <param name="knownPolarity"></param>
        /// <returns></returns>
        public bool determineSynsetPolarity(string knownWord, PartOfSpeech pos, Polarity knownPolarity)
        {
            IList<ILemma> senses = lexDict.FindSenses(knownWord, pos);

            if (senses == null || senses.Count == 0)
            {
                return false;
            }

            float totalFrequency = calculateTotalFrequency(knownWord, pos);

            //T1
            if (senses.Count == 2 && (senses[0].Frequency == senses[1].Frequency || senses[0].Frequency == 0))
            {
                //try
                //{
                if (!ht.ContainsKey(senses[0].Synset.SynsetId))
                    ht.Add(senses[0].Synset.SynsetId, new SynsetInfo(senses[0], knownPolarity, knownWord, null, "T1"));

                if (!ht.ContainsKey(senses[1].Synset.SynsetId))
                    ht.Add(senses[1].Synset.SynsetId, new SynsetInfo(senses[1], knownPolarity, knownWord, null, "T1"));
                //}
                //catch (ArgumentException ex)
                //{
                //Console.WriteLine(ex.Message);
                //}
                return true;
            }
            //T0
            else if ((senses[0].Frequency >= 0.5 * totalFrequency) || (senses[0].Frequency == 0 && 0.1 >= 0.5 * totalFrequency))
            {
                if (!ht.ContainsKey(senses[0].Synset.SynsetId))
                    ht.Add(senses[0].Synset.SynsetId, new SynsetInfo(senses[0], knownPolarity, knownWord, null, "T0"));

                return true;
            }

            return false;
            //return true;
        }

        /// <summary>
        /// /// Method uses Rule2 to deduce polarity of synsets
        /// </summary>
        /// <param name="knownWord"></param>
        /// <param name="pos"></param>
        /// <param name="knownPolarity"></param>
        /// <returns></returns>
        public bool determinSynsetPolarityByMinimalSet(string knownWord, PartOfSpeech pos, Polarity knownPolarity)
        {

            IList<ILemma> senses = lexDict.FindSenses(knownWord, pos);

            if (senses == null || senses.Count == 0)
            {
                return false;
                //throw new Exception("Word not found in database_2: " + knownWord + ":" + pos.ToString());
            }

            float totalFrequency = calculateTotalFrequency(knownWord, pos);

            ArrayList samePolaritySenses = new ArrayList();
            ArrayList oppPolaritySenses = new ArrayList();
            ILemma minFrequenceSense = null;
            int samePolaritySynsetFreq = 0;
            int oppPolaritySynsetFreq = 0;
            foreach (ILemma sense in senses)
            {
                if (ht[sense.SynsetId] == null)
                    return false;

                if (ht[sense.SynsetId] != null &&
                        ((SynsetInfo)ht[sense.SynsetId]).polarity != knownPolarity)
                {
                    oppPolaritySenses.Add(((SynsetInfo)ht[sense.SynsetId]));
                    oppPolaritySynsetFreq = oppPolaritySynsetFreq + ((SynsetInfo)ht[sense.SynsetId]).lemma.Frequency;
                }
                else
                {
                    samePolaritySenses.Add(sense);
                    samePolaritySynsetFreq = oppPolaritySynsetFreq + sense.Frequency;
                    if (minFrequenceSense == null) minFrequenceSense = sense;
                    else
                        if (minFrequenceSense.Frequency > sense.Frequency)
                            minFrequenceSense = sense;
                }

            }

            if (samePolaritySynsetFreq > (0.5 * oppPolaritySynsetFreq)
                    && ((samePolaritySynsetFreq - minFrequenceSense.Lemma.Frequency) <= (0.5 * oppPolaritySynsetFreq))
                )
            {
                //T is minimal, add all these synsets to hashtable
                foreach (ILemma sense in samePolaritySenses)
                {
                    // if ( ht[sense.Synset.SynsetId] == null )
                    if (!ht.ContainsKey(sense.Synset.SynsetId))
                        ht.Add(sense.Synset.SynsetId, new SynsetInfo(sense, knownPolarity, knownWord, null, "T3"));
                }
            }

            return true;

        }

        /// <summary>
        /// Methods uses Hyponyms to deduce polarity of synsets.
        /// </summary>
        public void AssignPolarityAddHyponyms()
        {

            while (true)
            {
                Hashtable newHt = new Hashtable();
                foreach (SynsetInfo syn in ht.Values)
                {

                    IList<ISynset> hyponyms = syn.lemma.Synset.RelatedSynsets(RelationType.Hyponym);

                    if (hyponyms.Count > 0)
                        if (!ht.ContainsKey(hyponyms[0].SynsetId) && !newHt.ContainsKey(hyponyms[0].SynsetId))
                        {
                            SynsetInfo s = new SynsetInfo(hyponyms[0].Lemma, syn.polarity, syn.rootWord, syn, "RelHyponym");
                            newHt.Add(hyponyms[0].SynsetId, s);
                            s.rootSynset = syn;
                        }


                    IList<ISynset> verbEnt = syn.lemma.Synset.RelatedSynsets(RelationType.VerbEntailment);
                    //IList<ILemma> hyponyms = lexDict.FindSenses("good", PartOfSpeech.Noun)[0].RelatedLemmas(RelationType.Hyponym);
                    foreach (ISynset hyp in verbEnt)
                    {
                        if (!ht.ContainsKey(hyp.SynsetId) && !newHt.ContainsKey(hyp.SynsetId))
                        {
                            SynsetInfo s = new SynsetInfo(hyp.Lemma, syn.polarity, syn.rootWord, syn, "RelVerbEntail");
                            newHt.Add(hyp.SynsetId, s);
                            s.rootSynset = syn;
                        }
                    }

                    IList<ILemma> antonyms = syn.lemma.RelatedLemmas(RelationType.Antonym);
                    //IList<ILemma> hyponyms = lexDict.FindSenses("good", PartOfSpeech.Noun)[0].RelatedLemmas(RelationType.Hyponym);
                    foreach (ILemma hyp in antonyms)
                    {
                        if (!ht.ContainsKey(hyp.Synset.SynsetId) && !newHt.ContainsKey(hyp.Synset.SynsetId))
                        {
                            SynsetInfo s = new SynsetInfo(hyp, InversePolarity(syn.polarity), syn.rootWord, syn, "RelAntonym");
                            newHt.Add(hyp.SynsetId, s);
                            s.rootSynset = syn;
                        }
                    }
                }

                if (newHt.Count == 0) break;

                foreach (SynsetInfo sInfo in newHt.Values)
                {
                    if (!ht.ContainsKey(sInfo.lemma.SynsetId))
                        ht.Add(sInfo.lemma.SynsetId, sInfo); //new SynsetInfo(sInfo.lemma, sInfo.polarity, sInfo.rootWord, sInfo.derivedCondition ));
                }
            }
        }

        #endregion
        
        //Logging functions
        public void printAllSynsets()
        {
            foreach (int key in ht.Keys)
            {
                SynsetInfo sInfo = (SynsetInfo)ht[key];
                //Console.WriteLine(key + ":" + sInfo.lemma.Synset.Definition );

                Console.WriteLine(sInfo.lemma.Text + ":" + sInfo.polarity.ToString()
                                                                        + ":" + sInfo.rootWord);

            }
        }
    }

   
}
