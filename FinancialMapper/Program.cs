using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinancialMapper
{
    using System.Collections.Concurrent;
    using System.Security.Cryptography.X509Certificates;
    using System.Web;

    using Newtonsoft.Json;

    class Program
    {
        public static ConcurrentDictionary<string,Tuple<double,double>> censusData = new ConcurrentDictionary<string, Tuple<double,double>>(); 

        public static ConcurrentDictionary<string, Dictionary<int,int>> wordIncomes = new ConcurrentDictionary<string, Dictionary<int,int>>(); 

        static void Main(string[] args)
        {
            int fileNumber = 0;
            
            BuildCensusData();

            for (int i = 0; i < 450; i++)
            {
                Console.WriteLine("file: " + i + " unique words: " + wordIncomes.Keys.Count);
                processFile(i);
            }

            List<string> lines = new List<string>();

            List<Tuple<int,string>> sortedIncomes = new List<Tuple<int, string>>();

            foreach (var word in wordIncomes.Keys)
            {
                List<Tuple<int,int>> values = new List<Tuple<int, int>>();

                foreach (var bucket in wordIncomes[word].Keys)
                {
                    values.Add(new Tuple<int, int>(bucket, wordIncomes[word][bucket]));
                }

                if (values.Count <= 5)
                {
                    continue;
                }

                int count = 0;

                List<long> amounts = new List<long>();

                foreach (var tuple in values)
                {
                    if (tuple.Item1 < 0 || tuple.Item2 < 0)
                    {
                        continue;
                    }
                    amounts.Add(tuple.Item1 * tuple.Item2);
                    count += tuple.Item2;
                }

                long total = amounts.Sum();

                if (count == 0)
                {
                    continue;
                }
                int income = (int)(total / count);

                sortedIncomes.Add(new Tuple<int, string>(income, word));

                

            }

            sortedIncomes = sortedIncomes.OrderBy(i => i.Item1).ToList();

            foreach (var sortedIncome in sortedIncomes)
            {
                lines.Add(sortedIncome.Item2 + " " + sortedIncome.Item1);    
            }
            
            File.WriteAllLines("C:\\Users\\tstapleton\\Desktop\\censusData\\wordIncomes.txt", lines);


            Console.WriteLine("process finished");
            Console.Read();
            
        }

        public static void processFile(int fileNumber)
        {
            var rawTweets = File.ReadAllLines(string.Format("C:\\Users\\tstapleton\\Desktop\\locationResults\\TweetFile{0}.tweet", fileNumber)).ToList();

            foreach (var rawTweet in rawTweets)
            {
                var tweet = JsonConvert.DeserializeObject<Tweet>(rawTweet);
                tweet.Text = HttpUtility.UrlDecode(tweet.Text);

                tweet.Text = new string(tweet.Text.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '@' || c == '#').ToArray()).ToLower();

                if (string.IsNullOrWhiteSpace(tweet.BlockGroup))
                {
                    continue;
                }

                var result = lookupCensus(tweet.BlockGroup);

                if (result == null)
                {
                    continue;
                }

                if (result.Item1 > 1000000)
                {
                    continue;
                }

                // 100 - ((income/standard deviation) x 100)
                int magic = (int)((100 - ((result.Item2 / result.Item1) * 100))+1);

                int bucket = (int)(result.Item1);


                foreach (var word in tweet.Text.Split(' '))
                {
                    if (word.StartsWith("http") || word.StartsWith("www") || word.StartsWith("@"))
                    {
                        continue;
                    }

                    var trimmedWord = word.Trim();

                    Dictionary<int,int> toGet;
                    wordIncomes.TryGetValue(trimmedWord, out toGet);
                    if (toGet == null)
                    {
                        toGet = new Dictionary<int, int>();
                    }

                    if (!toGet.ContainsKey(bucket))
                    {
                        toGet.Add(bucket, 0);
                    }

                    toGet[bucket] += magic;

                    wordIncomes[trimmedWord] = toGet;
                }

            }
        }

        public static Tuple<double, double> lookupCensus(string blockGroup)
        {
            string toLookup = blockGroup.Substring(0, blockGroup.Length - 3);

            Tuple<double, double> toGet;
            censusData.TryGetValue(toLookup, out toGet);

            return toGet;
        }

        public static void BuildCensusData()
        {
            var rawCensus = File.ReadAllLines("C:\\Users\\tstapleton\\Desktop\\censusData\\data.csv").ToList();

            foreach (var line in rawCensus)
            {
                var splits = line.Split(',');
                string fips = splits[7].Substring(1, splits[7].Length - 1).Substring(0, splits[7].Length - 2)
                    + splits[9].Substring(1, splits[9].Length - 1).Substring(0, splits[9].Length - 2)
                    + splits[14].Substring(1, splits[14].Length - 1).Substring(0, splits[14].Length - 2)
                    + splits[15].Substring(1, splits[15].Length - 1).Substring(0, splits[15].Length - 2);

                double income;

                if (string.IsNullOrWhiteSpace(splits[55]))
                {
                    continue;
                }

                income = double.Parse(splits[55]);

                double deviation;
                if(!string.IsNullOrWhiteSpace(splits[60]))
                {
                    deviation = double.Parse(splits[60]);
                }
                else
                {
                    deviation = 0;
                }

                censusData.TryAdd(fips, new Tuple<double, double>(income, deviation));
            }
        }
    }
}
