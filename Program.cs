using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LinqToTwitter;

namespace Retweeter
{
    class Program
    {
        private static readonly log4net.ILog m_logger =
            log4net.LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            m_logger.Info("■処理を開始します。");
            try
            {
                if (Properties.Settings.Default.IsReset)
                {
                    Properties.Settings.Default.Reset();
                }

                Program prg = new Program();
                prg.Execute();

                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                m_logger.Fatal("異常終了しました", ex);
            }
            m_logger.Info("■処理を終了します。");
        }

        /// <summary>
        /// 実行
        /// </summary>
        public void Execute()
        {
            //
            // get user credentials and instantiate TwitterContext
            //
            ITwitterAuthorization auth;

            if (String.IsNullOrEmpty(Properties.Settings.Default.UserID))
            {
                m_logger.Info("Skipping OAuth authorization demo because twitterConsumerKey and/or twitterConsumerSecret are not set in your .config file.");
                m_logger.Info("Using username/password authorization instead.");

                // For username/password authorization demo...
                auth = new UsernamePasswordAuthorization(Utilities.GetConsoleHWnd());
            }
            else
            {
                //Console.WriteLine("Discovered Twitter OAuth consumer key in .config file.  Using OAuth authorization.");

                m_logger.InfoFormat("Using '{0}'/**** authorization instead.", Properties.Settings.Default.UserID);

                auth = new UsernamePasswordAuthorization();
                var desktopAuth = (UsernamePasswordAuthorization)auth;
                desktopAuth.AllowUIPrompt = false;
                desktopAuth.UserName = Properties.Settings.Default.UserID;
                desktopAuth.Password = Properties.Settings.Default.Password;
            }

            // TwitterContext is similar to DataContext (LINQ to SQL) or ObjectContext (LINQ to Entities)

            // For Twitter
            using (var twitterCtx = new TwitterContext(auth, "https://api.twitter.com/1/", "http://search.twitter.com/"))
            {
                // If we're using OAuth, we need to configure it with the ConsumerKey etc. from the user.
                if (twitterCtx.AuthorizedClient is OAuthAuthorization)
                {
                    InitializeOAuthConsumerStrings(twitterCtx);
                }

                // Whatever authorization module we selected... sign on now.  
                // See the bottom of the method for sign-off procedures.
                try
                {
                    auth.SignOn();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Login canceled. Demo exiting.");
                    return;
                }

                // Search & Retweet
                SearchAndRetweet(twitterCtx);

                //
                // Sign-off, including optional clearing of cached credentials.
                //
                auth.SignOff();
            }
        }

        private void InitializeOAuthConsumerStrings(TwitterContext twitterCtx)
        {
            var oauth = (DesktopOAuthAuthorization)twitterCtx.AuthorizedClient;
            oauth.GetVerifier = () =>
            {
                Console.WriteLine("Next, you'll need to tell Twitter to authorize access.\nThis program will not have access to your credentials, which is the benefit of OAuth.\nOnce you log into Twitter and give this program permission,\n come back to this console.");
                Console.Write("Please enter the PIN that Twitter gives you after authorizing this client: ");
                return Console.ReadLine();
            };

            if (oauth.CachedCredentialsAvailable)
            {
                Console.WriteLine("Skipping OAuth authorization step because that has already been done.");
            }
        }

        /// <summary>
        /// shows how to perform a twitter search
        /// </summary>
        /// <param name="twitterCtx">TwitterContext</param>
        private void SearchAndRetweet(TwitterContext twitterCtx)
        {
            foreach (string creteria in Properties.Settings.Default.SearchWords)
            {
                var queryResults =
                    from search in twitterCtx.Search
                    where search.Type == SearchType.Search &&
                          search.Query == creteria &&
                          search.Page == 2 &&
                          search.PageSize == Properties.Settings.Default.PageSize
                    select search;

                foreach (var search in queryResults)
                {
                    // here, you can see that properties are named
                    // from the perspective of atom feed elements
                    // i.e. the query string is called Title
                    m_logger.Info("Query:" + search.Title);

                    IOrderedEnumerable<AtomEntry> entries = search.Entries.OrderBy(ent => ent.Published);
                    DateTime lastPublished = Properties.Settings.Default.LastPublished;

                    foreach (var entry in entries)
                    {
                        m_logger.InfoFormat("Date:{3}, ID:{0}, Source:{1}, Content:{2}",
                            entry.ID, entry.Source, entry.Content, entry.Published.ToString("yyyy/MM/dd HH:mm:ss"));

                        if (lastPublished < entry.Published)
                        {
                            string hitIgnoreSource = ContainsWord(Properties.Settings.Default.IgnoreSources, entry.Source);
                            string hitIgnoreWord = ContainsWord(Properties.Settings.Default.IgnoreWords, entry.Content);

                            if (!String.IsNullOrEmpty(hitIgnoreSource))
                            {
                                m_logger.InfoFormat("投稿元に '{0}' が含まれるためスキップしました。", hitIgnoreSource);
                            }
                            if (!String.IsNullOrEmpty(hitIgnoreWord))
                            {
                                m_logger.InfoFormat("本文に '{0}' が含まれるためスキップしました。", hitIgnoreWord);
                            }
                            else
                            {
                                twitterCtx.Retweet(entry.ID.Substring(entry.ID.LastIndexOf(':') + 1));
                                m_logger.InfoFormat("リツイートしました");
                            }

                            Properties.Settings.Default.LastPublished = entry.Published;
                            Properties.Settings.Default.Save();

                        }
                        else
                        {
                            m_logger.InfoFormat("前回投稿日より古いためスキップしました");
                        }
                    }
                }
            }
        }

        private string ContainsWord(System.Collections.Specialized.StringCollection words, string target)
        {
            foreach (string item in words)
            {
                if (target.Contains(item))
                {
                    return item;
                }
            }

            return null;
        }

    }
}
