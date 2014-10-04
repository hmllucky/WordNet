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
    class BFSData
    {
        static string connectionStr = Properties.connectionString;

        public static void AddOpFinderWords(Hashtable ht1,Hashtable ht2)
        {
            string insertStatement = @"INSERT INTO [SentiFindDB].[dbo].[OpFinderWordList]
           ([word]
           ,[pos]
           ,[type])
            VALUES
           ('{0}','{1}','{2}')";

            SqlConnection conn = new SqlConnection(connectionStr);

            string sql = string.Empty;
            conn.Open();

            foreach ( OpFinderWordInfo w in ht1.Values)
            {
                sql = string.Format(insertStatement, w.word, Util.ConvertFromOpFinderPOS(w.pos).ToString(), w.type);
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
           
            }

            foreach (OpFinderWordInfo w in ht2.Values)
            {
                sql = string.Format(insertStatement, w.word, Util.ConvertFromOpFinderPOS(w.pos).ToString(), w.type);
                               
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
             }

            conn.Close();            


        }

        
        public static void RecordsBFSWordsAndSynsets()
        {

            string deleteStatement = @"delete from [SentiFindDB].[dbo].[SynsetWordsMap]";
            SqlConnection conn = new SqlConnection(connectionStr);

            string sql = string.Empty;
            conn.Open();

            SqlCommand cmd = new SqlCommand(deleteStatement, conn);
            cmd.ExecuteNonQuery();

            conn.Close();

            OpinionFinderList.LoadOpinionFinderWordList(Properties.OpinionFinderFile, Properties.OpinionFinderTestFile);
            foreach (OpFinderWordInfo word in OpinionFinderList.opTrainingWordList.Values)
            {
                RecordsBFSWordsAndSynsets(word);
            }
        }


        public static void RecordsBFSWordsAndSynsets(OpFinderWordInfo opWInfo)
        {

            Dictionary<int, SynsetInfo> synInfo = new Dictionary<int, SynsetInfo>();
            Dictionary<string, WordInfo> wordInfo = new Dictionary<string, WordInfo>();

            int distance = 1;

            {
                //opWInfo = (OpFinderWordInfo)OpinionFinderList.opTrainingWordList[word];
                IList<ILemma> senses = (IList<ILemma>)LexiconDict.getLexiconDict().FindSenses(opWInfo.word, Util.ConvertFromOpFinderPOS(opWInfo.pos));
                //{
                //  IList<ILemma> senses = (IList<ILemma>)LexiconDict.getLexiconDict().FindSenses("fury", PartOfSpeech.Noun);

                foreach (ILemma sense in senses)
                {
                    if (!synInfo.ContainsKey(sense.SynsetId))
                    {
                        synInfo.Add(sense.SynsetId, new SynsetInfo(sense, opWInfo.p, opWInfo.word, "none"));
                        //synInfo.Add(sense.SynsetId, new SynsetInfo(sense, Polarity.Postive, "fury", "none"));
                    }

                    //Add synsets
                    //BFSData.AddSynset(opWInfo.word, Util.ConvertFromOpFinderPOS(opWInfo.pos), sense);

                    //}

                }
            }

            //Insert synsets into database  
            foreach (SynsetInfo synsetInfo in synInfo.Values)
            {
                foreach (ILemma words in synsetInfo.lemma.Synset.Lemmas)
                {
                    if (!(synsetInfo.lemma.Text.Equals(words.Text) && (synsetInfo.lemma.PartOfSpeech == words.PartOfSpeech)))
                    {
                        if (!wordInfo.ContainsKey(words.Text.ToLower() + ":" + words.PartOfSpeech.ToString() + ":" + synsetInfo.rootWord))
                        {
                            wordInfo.Add(words.Text.ToLower() + ":" + words.PartOfSpeech.ToString() + ":" + synsetInfo.rootWord.ToLower(), new WordInfo(words.Text, synsetInfo, distance));
                        }
                        //Add words

                    }
                }

            }



            int steps = 1;
            int tp = 0;
            foreach (OpFinderWordInfo opInfo in OpinionFinderList.opTrainingWordList.Values)
            {
                if (wordInfo.ContainsKey(opInfo.word + ":" + Util.ConvertFromOpFinderPOS(opInfo.pos)))
                    tp++;

            }
            Console.WriteLine(steps + ":" + OpinionFinderList.opTestWordList.Count + ":" + tp);


            //Clean up the list
            List<string> list = new List<string>();
            foreach (WordInfo wInfo in wordInfo.Values)
            {
                if (wInfo.word == wInfo.synInfo.rootWord)
                    list.Add(wInfo.word.ToLower() + ":" + wInfo.synInfo.lemma.PartOfSpeech + ":" + wInfo.synInfo.rootWord);
                //wordInfo.Remove(wInfo.word + ":" + wInfo.synInfo.lemma.PartOfSpeech);
            }

            foreach (string s in list)
            {
                //wordInfo.Remove(s);
            }


            //Now bootstrap
            Dictionary<int, SynsetInfo> synInfo_1 = new Dictionary<int, SynsetInfo>(synInfo);
            Dictionary<string, WordInfo> wordInfo_1 = new Dictionary<string, WordInfo>(wordInfo);
            while (true)
            {
                Dictionary<int, SynsetInfo> synInfo_2 = new Dictionary<int, SynsetInfo>();
                Dictionary<string, WordInfo> wordInfo_2 = new Dictionary<string, WordInfo>();

                foreach (WordInfo wInfo in wordInfo_1.Values)
                {
                    IList<ILemma> senses = (IList<ILemma>)LexiconDict.getLexiconDict().FindSenses(wInfo.word, wInfo.synInfo.lemma.PartOfSpeech);
                    foreach (ILemma word in senses)
                    {
                        if (!synInfo_2.ContainsKey(word.SynsetId))
                        {
                            synInfo_2.Add(word.SynsetId, new SynsetInfo(word, wInfo.p, wInfo.synInfo.rootWord, "none"));


                            //Add  synsets
                            //BFSData.AddSynset(   word.PartOfSpeech, word);

                        }

                        if (!synInfo.ContainsKey(word.SynsetId))
                            synInfo.Add(word.SynsetId, new SynsetInfo(word, wInfo.p, wInfo.synInfo.rootWord, "none"));

                    }

                    //if (synInfo_2.Count == 98)
                    //  SynsetClassifier.temp1 = synInfo_2;

                }

                if (synInfo_2.Count == 0)
                    break;

                distance++;

                if (distance > 5) break;

                foreach (SynsetInfo synsetInfo in synInfo_2.Values)
                {
                    foreach (ILemma words in synsetInfo.lemma.Synset.Lemmas)
                    {
                        if (!words.Text.ToLower().Equals(synsetInfo.lemma.Text.ToLower()) && !wordInfo.ContainsKey(words.Text.ToLower() + ":" + words.PartOfSpeech.ToString() + ":" + synsetInfo.rootWord))
                        {
                            if (!wordInfo_2.ContainsKey(words.Text.ToLower() + ":" + words.PartOfSpeech.ToString() + ":" + synsetInfo.rootWord))
                                wordInfo_2.Add(words.Text.ToLower() + ":" + words.PartOfSpeech.ToString() + ":" + synsetInfo.rootWord, new WordInfo(words.Text, synsetInfo, distance));

                            if (!wordInfo.ContainsKey(words.Text.ToLower() + ":" + words.PartOfSpeech.ToString() + ":" + synsetInfo.rootWord))
                                wordInfo.Add(words.Text.ToLower() + ":" + words.PartOfSpeech.ToString() + ":" + synsetInfo.rootWord, new WordInfo(words.Text, synsetInfo, distance));
                            //Add words
                        }
                    }
                }

                //Clean up the list
                list = new List<string>();
                foreach (WordInfo wInfo in wordInfo.Values)
                {
                    if (wInfo.word == wInfo.synInfo.rootWord)
                        list.Add(wInfo.word.ToLower() + ":" + wInfo.synInfo.lemma.PartOfSpeech + ":" + wInfo.synInfo.rootWord);
                    //wordInfo.Remove(wInfo.word + ":" + wInfo.synInfo.lemma.PartOfSpeech);
                }

                foreach (string s in list)
                {
                    // wordInfo.Remove(s);
                }

                if (wordInfo_2.Count == 0)
                    break;

                wordInfo_1 = wordInfo_2;
                tp = 0;
                steps++;
                foreach (OpFinderWordInfo opInfo in OpinionFinderList.opTestWordList.Values)
                {
                    if (wordInfo.ContainsKey(opInfo.word + ":" + Util.ConvertFromOpFinderPOS(opInfo.pos)))
                        tp++;

                }
                Console.WriteLine(steps + ":" + OpinionFinderList.opTestWordList.Count + ":" + tp);

            }

            Console.WriteLine("Training Data" + OpinionFinderList.opTrainingWordList.Count);

            //  RecordDiscoveredSynsets(synInfo);
            RecordDiscoveredWords(wordInfo);

        }


        public static void RecordDiscoveredSynsets(Dictionary<int, SynsetInfo> synsetInfo)
        {

            string deleteStatement = @"delete from [SentiFindDB].[dbo].[WordSynsetMap]";
            string insertStatement = @"INSERT INTO [SentiFindDB].[dbo].[WordSynsetMap]
           (
            [word]
           ,[pos]
           ,[synsetid]
           ,[rootword])
            VALUES
           ('{0}','{1}','{2}','{3}')";

            SqlConnection conn = new SqlConnection(connectionStr);

            string sql = string.Empty;
            conn.Open();

            SqlCommand cmd = new SqlCommand(deleteStatement, conn);
            cmd.ExecuteNonQuery();

            foreach (SynsetInfo sInfo in synsetInfo.Values)
            {
                sql = string.Format(insertStatement, sInfo.lemma.Text.Replace("'","''").ToLower(), sInfo.lemma.PartOfSpeech.ToString(),sInfo.lemma.SynsetId, sInfo.rootWord.Replace("'","''").ToLower() );

                cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            conn.Close();            
        }

        public static void RecordDiscoveredWords( Dictionary<string, WordInfo> wordInfo)
        {


            string deleteStatement = @"delete from [SentiFindDB].[dbo].[SynsetWordsMap]";
            string insertStatement = @"INSERT INTO [SentiFindDB].[dbo].[SynsetWordsMap]
           ([synsetid]
           ,[word]
           ,[pos]
           ,[rootword]
           ,[distance])
            VALUES
           ({0},'{1}','{2}','{3}',{4})";

            SqlConnection conn = new SqlConnection(connectionStr);

            string sql = string.Empty;
            conn.Open();

            SqlCommand cmd = new SqlCommand(deleteStatement, conn);
            //cmd.ExecuteNonQuery();

            foreach (WordInfo wInfo in wordInfo.Values)
            {
                sql = string.Format(insertStatement, wInfo.synInfo.lemma.SynsetId, wInfo.word.Replace("'", "''").ToLower(), wInfo.synInfo.lemma.PartOfSpeech.ToString(), wInfo.synInfo.rootWord.Replace("'", "''").ToLower(), wInfo.distanceFromRootWord);

                cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }

            conn.Close();            


        }





    }

    class ResultsDB
    {
        static string connectionStr = Properties.connectionString;

        #region "variable"
        int experimentNo;
        string experimentName;
        int trialNo;
        string split;
        Hashtable htTraining;
        Hashtable htTest;
        Hashtable htNewWordList;
        ArrayList htCorrectList;
        ArrayList htInCorrectList;

        int trainOpFinderPos = 0;
        int trainOpFinderNegative = 0;
        int trainOpFinderNeutral = 0;

        int testOpFinderPos = 0;
        int testOpFinderNeg = 0;
        int testOpFinderNeutral = 0;

        int resultPos = 0;
        int resultNeg = 0;
        int resultNeutral = 0;

        int correctCountPos = 0;
        int correctCountNeg = 0;
        int correctCountNeutral = 0;

        int inCorrectCountPos = 0;
        int inCorrectCountNeg = 0;
        int inCorrectCountNeutral = 0;
        #endregion

        public ResultsDB(int experimentNo,
                                        string experimentName,
                                        int trialNo,
                                        string split, Hashtable htTraining,
                                        Hashtable htTest, Hashtable htNewWordList, ArrayList htCorrectList, ArrayList htInCorrectList)
        {

            this.experimentNo = experimentNo;
            this.experimentName = experimentName;
            this.trialNo = trialNo;
            this.split = split;
            this.htTraining = htTraining;
            this.htTest = htTest;
            this.htNewWordList = htNewWordList;
            this.htCorrectList = htCorrectList;
            this.htInCorrectList = htInCorrectList;

            CalculateTotals();


        }

        public void CalculateTotals()
        {
            ResultsDB.calculatePolarityCountOpFinder(htTraining, out trainOpFinderPos, out trainOpFinderNegative, out trainOpFinderNeutral);
            ResultsDB.calculatePolarityCountOpFinder(htTest, out testOpFinderPos, out testOpFinderNeg, out testOpFinderNeutral);
            ResultsDB.calculatePolarityOfResults(htNewWordList, out resultPos, out resultNeg, out resultNeutral);
            ResultsDB.calculatePrecisionCount(htCorrectList, out correctCountPos, out correctCountNeg, out correctCountNeutral);
            ResultsDB.calculatePrecisionCount(htInCorrectList, out inCorrectCountPos, out inCorrectCountNeg, out inCorrectCountNeutral);

        }

        public static void calculatePolarityCountOpFinder(Hashtable ht, out int pos, out int neg, out int neutral)
        {
            pos = neg = neutral = 0;
            foreach (OpFinderWordInfo info in ht.Values)
            {
                if (info.p == Polarity.Postive) pos++;
                if (info.p == Polarity.Negative) neg++;
                if (info.p == Polarity.Neutral) neutral++;
            }
        }

        public static void calculatePolarityOfResults(Hashtable ht, out int pos, out int neg, out int neutral)
        {
            pos = neg = neutral = 0;
            foreach (WordInfo info in ht.Values)
            {
                if (info.p == Polarity.Postive) pos++;
                if (info.p == Polarity.Negative) neg++;
                if (info.p == Polarity.Neutral) neutral++;
            }
        }

        public static void calculatePrecisionCount(ArrayList list, out int pos, out int neg, out int neutral)
        {
            pos = neg = neutral = 0;
            foreach (WordInfo info in list)
            {
                if (info.p == Polarity.Postive) pos++;
                if (info.p == Polarity.Negative) neg++;
                if (info.p == Polarity.Neutral) neutral++;
            }
        }

        public void PrintResults()
        {
            string header = string.Empty;
            if (!File.Exists("results.txt"))
                header = "Split Total-OpFinder(pos/neg/neutral) Test-OpFinder(pos/neg/neutral) Total-Results(pos/neg/neu) #Correct(pos/neg/neu) #InCorrect(pos/neg/neu)";


            StreamWriter writer = new StreamWriter("results.txt", true);
            writer.WriteLine(header);

            writer.Write(split);
            writer.Write(" ");

            writer.Write(trainOpFinderPos);
            writer.Write(" ");
            writer.Write(trainOpFinderNegative);
            writer.Write(" ");
            writer.Write(trainOpFinderNeutral);
            writer.Write(" ");

            writer.Write(testOpFinderPos);
            writer.Write(" ");
            writer.Write(testOpFinderNeg);
            writer.Write(" ");
            writer.Write(testOpFinderNeutral);
            writer.Write(" ");

            writer.Write(resultPos);
            writer.Write(" ");
            writer.Write(resultNeg);
            writer.Write(" ");
            writer.Write(resultNeutral);
            writer.Write(" ");

            writer.Write(correctCountPos);
            writer.Write(" ");
            writer.Write(correctCountNeg);
            writer.Write(" ");
            writer.Write(correctCountNeutral);
            writer.Write(" ");


            writer.Write(inCorrectCountPos);
            writer.Write(" ");
            writer.Write(inCorrectCountNeg);
            writer.Write(" ");
            writer.Write(inCorrectCountNeutral);
            writer.Write(" ");


            writer.Flush();
            writer.Close();

        }

        public static int GetLastExperimentNo()
        {

            SqlConnection conn = new SqlConnection(connectionStr);
            conn.Open();
            SqlCommand cmd = new SqlCommand("select max(experimentNo) from results", conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int x = (int)reader[0];
                conn.Close();
                return x;
            }
            return 0;
        }

        public void UpdateWordResults(int experimentId)
        {

            string insertStatement = @" INSERT INTO [SentiFindDB].[dbo].[ResultDetails]
           ([ExperimentId]
           ,[ResultValue]
           ,[Word]
           ,[InferredPolarity]
           ,[RootWord]
           ,[RootWordPolarity]
           ,[ConditionUsed]
           ,[OpFinderPolarity])
            VALUES ( {0}, {1}, '{2}','{3}',
                        '{4}', '{5}','{6}','{7}')";

            SqlConnection conn = new SqlConnection(connectionStr);
            conn.Open();

            foreach (WordInfo wInfo in htCorrectList)
            {
                //WordInfo wInfo = (WordInfo)htNewWordList[word];

                string stmt = string.Format(insertStatement,
                                                    experimentId,
                                                    1,
                                                    wInfo.word,
                                                    wInfo.p,
                                                    wInfo.synInfo.rootWord,
                                                    ((htTraining[wInfo.synInfo.rootWord + ":" + wInfo.synInfo.lemma.PartOfSpeech.ToString()]==null)?"":((OpFinderWordInfo)htTraining[wInfo.synInfo.rootWord + ":" + wInfo.synInfo.lemma.PartOfSpeech.ToString()]).p.ToString()),
                                                    wInfo.derivedCondition,
                                                    ((OpFinderWordInfo)htTest[wInfo.word + ":" + wInfo.synInfo.lemma.PartOfSpeech]).p.ToString()
                                                   );

                
                SqlCommand cmd = new SqlCommand(stmt, conn);
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                }

            }


            foreach (WordInfo wInfo in htInCorrectList)
            {
                //WordInfo wInfo = (WordInfo)htNewWordList[word];
                string stmt = string.Format(insertStatement,
                                                    experimentId,
                                                    0,
                                                    wInfo.word,
                                                    wInfo.p,
                                                    wInfo.synInfo.rootWord,
                                                    ((htTraining[wInfo.synInfo.rootWord + ":" + wInfo.synInfo.lemma.PartOfSpeech.ToString()] == null) ? "" : ((OpFinderWordInfo)htTraining[wInfo.synInfo.rootWord + ":" + wInfo.synInfo.lemma.PartOfSpeech.ToString()]).p.ToString()),
                                                    wInfo.derivedCondition,
                                                    ((OpFinderWordInfo)htTest[wInfo.word + ":" + wInfo.synInfo.lemma.PartOfSpeech]).p.ToString()
                                                   );

                SqlCommand cmd = new SqlCommand(stmt, conn);
                cmd.ExecuteNonQuery();

            }



        }

        public void insertResults()
        {
            string insertStatement = @"INSERT INTO [SentiFindDB].[dbo].[Results] 
           (
            [ExperimentNo]
            ,[TrialNo]
           ,[ExperimentType]
           ,[TrainSplits]
           ,[TrainingPos]
           ,[TrainingNeg]
           ,[TrainingNeutral]
           ,[TestPos]
           ,[TestNeg]
           ,[TestNeutral]
           ,[CorrestPos]
           ,[CorrectNeg]
           ,[CorrectNeutral]
           ,[IncorrectPos]
           ,[IncorrectNeg]
           ,[IncorrectNeutral]
            ,[NewWordsTotal])
            VALUES ( {0},{1},'{2}','{3}','{4}',
                           '{5}','{6}','{7}',
                           '{8}','{9}','{10}',
                           '{11}','{12}','{13}','{14}','{15}','{16}')";

            SqlConnection conn = new SqlConnection(connectionStr);
            string stmt = string.Format(insertStatement, experimentNo, trialNo, experimentName, split,
                                                        trainOpFinderPos, trainOpFinderNegative, trainOpFinderNeutral,
                                                        testOpFinderPos, testOpFinderNeg, testOpFinderNeutral,
                                                        correctCountPos, correctCountNeg, correctCountNeutral,
                                                        inCorrectCountPos, inCorrectCountNeg, inCorrectCountNeutral, htNewWordList.Count);

            conn.Open();
            SqlCommand cmd = new SqlCommand(stmt, conn);
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT IDENT_CURRENT('results')";
            SqlDataReader reader = cmd.ExecuteReader();
            reader.Read();
            decimal experimentId = (decimal)reader[0];

            UpdateWordResults((int)experimentId);


            conn.Close();
        }


    }
}
