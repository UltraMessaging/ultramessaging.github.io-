using System;
using System.Runtime.InteropServices;
using com.latencybusters.lbm;

namespace LBMApplication
{
    class umercv
    {
        [DllImport("Kernel32.dll")]
        public static extern int SetEnvironmentVariable(string name, string value);
        private static int dereg = 0;
        private const int DEFAULT_MAX_NUM_SRCS = 10000;
        private static int nstats = 10;
        private static int reap_msgs = 0;
        private static int stat_secs = 0;
        private static bool eventq = false;
        private static bool verbose = false;
        private static bool end_on_eos = false;
        private static bool summary = false;
        private static bool compat10 = false;
        private static int exack = 0;
        private static string purpose = "Purpose: Receive messages on a single topic.";
        private static string usage =
            "Usage: umercv [options] topic\n"
            + "Available options:\n"
            + "  -1 = act as a UME 1.2 and earlier receiver would act\n"
            + "  -c filename = read config file filename\n"
            + "  -d qdelay = monitor event queue delay above qdelay usecs\n"
            + "  -E = exit after source ends\n"
            + "  -D = Deregister after 1000 messages\n"
            + "  -e num_messages = send an Explicit ACK every num_messages messages\n"
            + "  -i offset = use offset to calculate Registration ID\n"
            + "              (as source registration ID + offset)\n"
            + "              offset of 0 forces creation of regid by store\n"
            + "  -N offset = display recovery sequence number info and set low seqnum to low+offset\n"
            + "  -S = exit after source ends, print throughput summary\n"
            + "  -s num_secs = print statistics every num_secs along with bandwidth\n"
            + "  -h = help\n"
            + "  -n nsrcs = stop topic resolution queries after nsrcs sources\n"
            + "  -q = use an LBM event queue\n"
            + "  -z qsize = monitor event queue size above qsize in length\n"
            + "  -r msgs = delete receiver after msgs messages\n"
            + "  -v = be verbose about each message\n"
            + "\nMonitoring options:\n"
            + "  --monitor-ctx NUM = monitor context every NUM seconds\n"
            + "  --monitor-rcv NUM = monitor receiver every NUM seconds\n"
            + "  --monitor-transport TRANS = use monitor transport module TRANS\n"
            + "                              TRANS may be `lbm', `udp', or `lbmsnmp', default is `lbm'\n"
            + "  --monitor-transport-opts OPTS = use OPTS as transport module options\n"
            + "  --monitor-format FMT = use monitor format module FMT\n"
            + "                         FMT may be `csv'\n"
            + "  --monitor-format-opts OPTS = use OPTS as format module options\n"
            + "  --monitor-appid ID = use ID as application ID string\n"
            ;

        static void Main(string[] args)
        {
            if (System.Environment.GetEnvironmentVariable("LBM_LICENSE_FILENAME") == null
                && System.Environment.GetEnvironmentVariable("LBM_LICENSE_INFO") == null)
            {
                SetEnvironmentVariable("LBM_LICENSE_FILENAME", "lbm_license.txt");
            }
            LBM lbm = new LBM();
            lbm.setLogger(new LBMLogging(logger));

            LBMObjectRecycler objRec = new LBMObjectRecycler();

            string conffname = null;
            string qdelay = null;
            string qsize = null;
            string num_srcs = null;
            int i;
            int n = args.Length;

            bool monitor_context = false;
            int monitor_context_ivl = 0;
            
            bool monitor_receiver = false;
            int monitor_receiver_ivl = 0;
            string application_id = null;
            int mon_format = LBMMonitor.FORMAT_CSV;
            int mon_transport = LBMMonitor.TRANSPORT_LBM;
            string mon_format_options = null;
            string mon_transport_options = null;
            bool error = false;
            bool done = false;
            string regid_offset = null;
            int seqnum_offset = 0;
            bool sqn_info = false;
            const string OPTION_MONITOR_CTX = "--monitor-ctx";
            const string OPTION_MONITOR_RCV = "--monitor-rcv";
            const string OPTION_MONITOR_TRANSPORT = "--monitor-transport";
            const string OPTION_MONITOR_TRANSPORT_OPTS = "--monitor-transport-opts";
            const string OPTION_MONITOR_FORMAT = "--monitor-format";
            const string OPTION_MONITOR_FORMAT_OPTS = "--monitor-format-opts";
            const string OPTION_MONITOR_APPID = "--monitor-appid";
            for (i = 0; i < n; i++)
            {
                try
                {
                    switch (args[i])
                    {
                        case OPTION_MONITOR_APPID:
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            application_id = args[i];
                            break;

                        case OPTION_MONITOR_CTX:
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            monitor_context = true;
                            monitor_context_ivl = Convert.ToInt32(args[i]);
                            break;

                        case OPTION_MONITOR_RCV:
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            monitor_receiver = true;
                            monitor_receiver_ivl = Convert.ToInt32(args[i]);
                            break;

                        case OPTION_MONITOR_FORMAT:
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            if (args[i].ToLower().CompareTo("csv") == 0)
                                mon_format = LBMMonitor.FORMAT_CSV;
                            else
                            {
                                error = true;
                                break;
                            }
                            break;

                        case OPTION_MONITOR_TRANSPORT:
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            if (args[i].ToLower().CompareTo("lbm") == 0)
                                mon_transport = LBMMonitor.TRANSPORT_LBM;
                            else if (args[i].ToLower().CompareTo("udp") == 0)
                                mon_transport = LBMMonitor.TRANSPORT_UDP;
                            else if (args[i].ToLower().CompareTo("lbmsnmp") == 0)
                                mon_transport = LBMMonitor.TRANSPORT_LBMSNMP;
                            else
                            {
                                error = true;
                                break;
                            }
                            break;

                        case OPTION_MONITOR_TRANSPORT_OPTS:
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            mon_transport_options += args[i];
                            break;

                        case OPTION_MONITOR_FORMAT_OPTS:
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            mon_format_options += args[i];
                            break;

                        case "-c":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            conffname = args[i];
                            break;

                        case "-1":
                            compat10 = true;
                            break;

                        case "-d":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            qdelay = args[i];
                            eventq = true;
                            break;

                        case "-E":
                            end_on_eos = true;
                            break;

                        case "-D":
                            dereg = 1;
                            break;

                        case "-e":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            exack = Convert.ToInt32(args[i]);
                            break;

                        case "-i":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            regid_offset = args[i];
                            break;

                        case "-h":
                            print_help_exit(0);
                            break;

                        case "-n":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            num_srcs = args[i];
                            break;

                        case "-N":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            seqnum_offset = Convert.ToInt32(args[i]);
                            sqn_info = true;
                            break;

                        case "-q":
                            eventq = true;
                            break;

                        case "-r":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            reap_msgs = Convert.ToInt32(args[i]);
                            break;

                        case "-S":
                            end_on_eos = true;
                            summary = true;
                            break;

                        case "-s":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            stat_secs = Convert.ToInt32(args[i]);
                            break;

                        case "-v":
                            verbose = true;
                            break;

                        case "-z":
                            if (++i >= n)
                            {
                                error = true;
                                break;
                            }
                            qsize = args[i];
                            eventq = true;
                            break;

                        default:
                            if (args[i].StartsWith("-"))
                            {
                                error = true;
                            }
                            else
                            {
                                done = true;
                            }
                            break;
                    }
                    if (error || done)
                        break;
                }
                catch (Exception e)
                {
                    /* type conversion exception */
                    System.Console.Error.WriteLine("umercv: error\n" + e.Message);
                    print_help_exit(1);
                }
            }
            if (error || i >= n)
            {
                /* An error occurred processing the command line - print help and exit */
                print_help_exit(1);
            }
            if (conffname != null)
            {
                LBM.setConfiguration(conffname);
            }
            LBMContextAttributes cattr = new LBMContextAttributes();
            cattr.setObjectRecycler(objRec, null);
            LBMContext ctx = new LBMContext(cattr);
            LBMReceiverAttributes rattr = null;
            UMERegistrationId umeregid;
            UMERegistrationIdCompat10 umeregidcompat10;
            rattr = new LBMReceiverAttributes();
            rattr.setObjectRecycler(objRec, null);
            if ((num_srcs != null || regid_offset != null) && compat10)
            {
                if (num_srcs != null)
                    rattr.setValue("resolution_number_of_sources_query_threshold", num_srcs);
                if (regid_offset != null)
                {
                    umeregidcompat10 = new UMERegistrationIdCompat10(regid_offset);
                    rattr.setRegistrationIdCallback(new LBMRegistrationIdCallback(umeregidcompat10.setRegistrationId), null);
                    System.Console.Out.WriteLine("Will use RegID offset " + regid_offset + ".");
                }
                else
                    System.Console.Out.WriteLine("Will not use specific RegID.");
            }
            else if (num_srcs != null || regid_offset != null)
            {
                if (num_srcs != null)
                    rattr.setValue("resolution_number_of_sources_query_threshold", num_srcs);
                if (regid_offset != null)
                {
                    umeregid = new UMERegistrationId(regid_offset);
                    rattr.setRegistrationIdCallback(new UMERegistrationIdExCallback(umeregid.setRegistrationId), null);
                    System.Console.Out.WriteLine("Will use RegID offset " + regid_offset + ".");
                }
                else
                    System.Console.Out.WriteLine("Will not use specific RegID.");
            }

            if (sqn_info)
            {
                UMERcvRecInfo umerecinfocb = new UMERcvRecInfo(seqnum_offset);
                rattr.setRecoverySequenceNumberCallback(umerecinfocb.setRecoverySequenceNumberInfo, null);
                System.Console.Out.WriteLine("Will use seqnum info with low offset " + seqnum_offset + ".");
            }

            if (exack > 0)
            {
                try
                {
                    rattr.setValue("ume_explicit_ack_only", "1");
                }
                catch (LBMException e)
                {
                    System.Console.Error.WriteLine("Error setting ume_explicit_ack_only=" + exack + e.Message);
                    System.Environment.Exit(1);
                }
            }

            LBMTopic topic = ctx.lookupTopic(args[i], rattr);
            UMERcvReceiver rcv = new UMERcvReceiver(verbose, end_on_eos, summary, exack, dereg);
            LBMReceiver lbmrcv;
            UMERcvEventQueue evq = null;
            if (eventq)
            {
                System.Console.Error.WriteLine("Event queue in use");
                LBMEventQueueAttributes evqattr = null;
                if (qsize != null || qdelay != null)
                {
                    evqattr = new LBMEventQueueAttributes();
                    if (qdelay != null)
                        evqattr.setValue("queue_delay_warning", qdelay);
                    if (qsize != null)
                        evqattr.setValue("queue_size_warning", qsize);
                }
                evq = new UMERcvEventQueue(evqattr);
                lbmrcv = new LBMReceiver(ctx, topic, rcv.onReceive, null, evq);
                ctx.enableImmediateMessageReceiver(evq);
            }
            else
            {
                System.Console.Error.WriteLine("No event queue");
                lbmrcv = new LBMReceiver(ctx, topic, rcv.onReceive, null);
                ctx.enableImmediateMessageReceiver();
            }

			rcv.setRcvr(lbmrcv);

            // This immediate-mode receiver is *only* used for topicless
            // immediate-mode sends.  Immediate sends that use a topic
            // are received with normal receiver objects.
            ctx.addImmediateMessageReceiver(new LBMImmediateMessageCallback(rcv.onReceive));

            LBMMonitorSource lbmmonsrc = null;
            if (monitor_context || monitor_receiver)
            {
                lbmmonsrc = new LBMMonitorSource(mon_format, mon_format_options, mon_transport, mon_transport_options);
                if (monitor_context)
                    lbmmonsrc.start(ctx, application_id, monitor_context_ivl);
                else
                    lbmmonsrc.start(lbmrcv, application_id, monitor_receiver_ivl);
            }
			System.Console.Out.Flush();
            long start_time;
            long end_time;
            long last_lost = 0, lost_tmp = 0, lost = 0;
            bool have_stats;
            LBMReceiverStatistics stats = null;
            long stat_time = System.DateTime.Now.AddSeconds(stat_secs).Ticks;
            for (; ; )
            {
                start_time = System.DateTime.Now.Ticks;
                if (eventq)
                {
                    evq.run(1000);
                }
                else
                {
                    System.Threading.Thread.Sleep(1000);
                }

                have_stats = false;
                while (!have_stats)
                {
                    try
                    {
                        stats = lbmrcv.getStatistics(nstats);
                        have_stats = true;
                    }
                    catch (LBMException ex)
                    {
                        /* Double the number of stats passed to the API to be retrieved */
                        /* Do so until we retrieve stats successfully or hit the max limit */
                        nstats *= 2;
                        if (nstats > DEFAULT_MAX_NUM_SRCS)
                        {
                            System.Console.Error.WriteLine("Error getting receiver statistics: " + ex.Message);
                            System.Environment.Exit(1);
                        }
                        /* have_stats is still false */
                    }
                }

                /* If we get here, we have the stats */
                lost = 0;
                for (i = 0; i < stats.size(); i++)
                {
                    lost += stats.lost(i);
                }

                /* Account for loss in previous iteration */
                lost_tmp = lost;
                if (last_lost <= lost)
                {
                    lost -= last_lost;
                }
                else
                {
                    lost = 0;
                }
                last_lost = lost_tmp;

                end_time = System.DateTime.Now.Ticks;

                print_bw((end_time - start_time) / 10000,
                    rcv.msg_count,
                    rcv.byte_count,
                    rcv.unrec_count,
                    lost,
                    rcv.burst_loss,
                    rcv.rx_msgs,
                    rcv.otr_msgs);

                if (stat_secs > 0 && stat_time <= end_time)
                {
                    stat_time = System.DateTime.Now.AddSeconds(stat_secs).Ticks;
                    print_stats(stats, evq);
                }

                objRec.doneWithReceiverStatistics(stats);

                rcv.msg_count = 0;
                rcv.byte_count = 0;
                rcv.unrec_count = 0;
                rcv.burst_loss = 0;
                rcv.rx_msgs = 0;
                rcv.otr_msgs = 0;

                if (reap_msgs != 0 && rcv.total_msg_count >= reap_msgs)
                {
                    break;
                }
            }

            System.Console.Error.WriteLine("Quitting.... received "
                + rcv.total_msg_count
                + " messages");
            objRec.close();
            lbmrcv.close();
            ctx.close();
            if (eventq)
                evq.close();
            GC.KeepAlive(lbm);
        }

        private static void print_help_exit(int exit_value)
        {
            System.Console.Error.WriteLine(LBM.version());
            System.Console.Error.WriteLine(purpose);
            System.Console.Error.WriteLine(usage);
            System.Environment.Exit(exit_value);
        }

        private static void logger(int loglevel, string message)
        {
            string level;
            switch (loglevel)
            {
                case LBM.LOG_ALERT: level = "Alert"; break;
                case LBM.LOG_CRIT: level = "Critical"; break;
                case LBM.LOG_DEBUG: level = "Debug"; break;
                case LBM.LOG_EMERG: level = "Emergency"; break;
                case LBM.LOG_ERR: level = "Error"; break;
                case LBM.LOG_INFO: level = "Info"; break;
                case LBM.LOG_NOTICE: level = "Note"; break;
                case LBM.LOG_WARNING: level = "Warning"; break;
                default: level = "Unknown"; break;
            }
            System.Console.Out.WriteLine(System.DateTime.Now.ToString() + " [" + level + "]: " + message);
			System.Console.Out.Flush();
        }

        private static int sourceNotification(string topic, string source, object cbArg)
        {
            System.Console.Error.WriteLine("new topic ["
                                            + topic
                                            + "], source ["
                                            + source
                                            + "]");
			System.Console.Out.Flush();
            return 0;

        }

        private static void print_stats(LBMReceiverStatistics stats, LBMEventQueue evq)
        {
            if (evq != null)
            {
                if (Convert.ToInt32(evq.getAttributeValue("queue_size_warning")) > 0)
                {
                    System.Console.Out.WriteLine("Event queue size: " + evq.size());
                }
            }
            for (int i = 0; i < stats.size(); i++)
            {
                switch (stats.type(i))
                {
                    case LBM.TRANSPORT_STAT_TCP:
                        System.Console.Out.WriteLine("TCP, source " + stats.source(i)
                                                     + ", received "
                                                     + stats.messagesReceived(i)
                                                     + "/"
                                                     + stats.bytesReceived(i)
                                                     + ", no topics "
                                                     + stats.noTopicMessagesReceived(i)
                                                     + ", requests "
                                                     + stats.lbmRequestsReceived(i));
                        break;
                    case LBM.TRANSPORT_STAT_LBTRU:
                    case LBM.TRANSPORT_STAT_LBTRM:
                        if (stats.type() == LBM.TRANSPORT_STAT_LBTRU)
                            System.Console.Out.Write("LBT-RU");
                        else
                            System.Console.Out.Write("LBT-RM");
                        System.Console.Out.WriteLine(", source " + stats.source(i)
                                                     + ", received "
                                                     + stats.messagesReceived(i)
                                                     + "/"
                                                     + stats.bytesReceived(i)
                                                     + ", naks "
                                                     + stats.nakPacketsSent(i)
                                                     + "/"
                                                     + stats.naksSent(i)
                                                     + ", lost "
                                                     + stats.lost(i)
                                                     + ", ncfs "
                                                     + stats.ncfsIgnored(i)
                                                     + "/"
                                                     + stats.ncfsShed(i)
                                                     + "/"
                                                     + stats.ncfsRetransmissionDelay(i)
                                                     + "/"
                                                     + stats.ncfsUnknown(i)
                                                     + ", recovery "
                                                     + stats.minimumRecoveryTime(i)
                                                     + "/"
                                                     + stats.meanRecoveryTime(i)
                                                     + "/"
                                                     + stats.maximumRecoveryTime(i)
                                                     + ", nak tx "
                                                     + stats.minimumNakTransmissions(i)
                                                     + "/"
                                                     + stats.minimumNakTransmissions(i)
                                                     + "/"
                                                     + stats.maximumNakTransmissions(i)
                                                     + ", dup "
                                                     + stats.duplicateMessages(i)
                                                     + ", unrecovered "
                                                     + stats.unrecoveredMessagesWindowAdvance(i)
                                                     + "/"
                                                     + stats.unrecoveredMessagesNakGenerationTimeout(i)
                                                     + ", LBM msgs " + stats.lbmMessagesReceived(i)
                                                     + ", no topics "
                                                     + stats.noTopicMessagesReceived(i)
                                                     + ", requests "
                                                     + stats.lbmRequestsReceived(i));
                        break;
                    case LBM.TRANSPORT_STAT_LBTIPC:
                        System.Console.Out.WriteLine("LBT-IPC, source "
                            + stats.source(i)
                            + ", received "
                            + stats.messagesReceived(i)
                            + "/"
                            + stats.bytesReceived(i)
                            + ", LBM msgs "
                            + stats.lbmMessagesReceived(i)
                            + ", no topics "
                            + stats.noTopicMessagesReceived(i)
                            + ", requests "
                            + stats.lbmRequestsReceived(i));
                        break;
                    case LBM.TRANSPORT_STAT_LBTRDMA:
                        System.Console.Out.WriteLine("LBT-RDMA, source "
                            + stats.source(i)
                            + ", received "
                            + stats.messagesReceived(i)
                            + "/"
                            + stats.bytesReceived(i)
                            + ", LBM msgs "
                            + stats.lbmMessagesReceived(i)
                            + ", no topics "
                            + stats.noTopicMessagesReceived(i)
                            + ", requests "
                            + stats.lbmRequestsReceived(i));
                        break;
                }
            }
			System.Console.Out.Flush();
        }

        private static void print_bw(long msec, long msgs, long bytes, long unrec, long lost, long burst_loss, long rx_msgs, long otr_msgs)
        {
            double sec;
            double mps = 0.0, bps = 0.0;
            double kscale = 1000.0, mscale = 1000000.0;
            char mgscale = 'K', bscale = 'K';

            sec = msec / 1000.0;
            if (sec == 0) return; /* avoid division by zero */
            mps = ((double)msgs) / sec;
            bps = ((double)bytes * 8) / sec;
            if (mps <= mscale)
            {
                mgscale = 'K';
                mps /= kscale;
            }
            else
            {
                mgscale = 'M';
                mps /= mscale;
            }
            if (bps <= mscale)
            {
                bscale = 'K';
                bps /= kscale;
            }
            else
            {
                bscale = 'M';
                bps /= mscale;
            }

            if ((rx_msgs > 0) || (otr_msgs > 0))
            {
                System.Console.Out.Write(sec
                + " secs. "
                + mps.ToString("0.000")
                + " " + mgscale
                + "msgs/sec. "
                + bps.ToString("0.000")
                + " " + bscale
                + "bps"
                + " [RX: " + rx_msgs + "][OTR: " + otr_msgs + "]");
            }
            else
            {
                System.Console.Out.Write(sec
                + " secs. "
                + mps.ToString("0.000")
                + " " + mgscale
                + "msgs/sec. "
                + bps.ToString("0.000")
                + " " + bscale
                + "bps");
            }

            if (lost != 0 || unrec != 0 || burst_loss != 0)
            {
                System.Console.Out.Write(" ["
                    + lost
                    + " pkts lost, "
                    + unrec
                    + " msgs unrecovered, "
                    + burst_loss
                    + " bursts]");
            }
            System.Console.Out.WriteLine("");
			System.Console.Out.Flush();
        }
    }

    class UMERcvEventQueue : LBMEventQueue
    {
        public UMERcvEventQueue()
            : this(null)
        {
        }

        public UMERcvEventQueue(LBMEventQueueAttributes evqattr)
            : base(evqattr)
        {
            this.addMonitor(new LBMEventQueueCallback(monitor));
        }

        protected void monitor(object cbarg, int evtype, int evq_size, long evq_delay)
        {
            System.Console.Error.WriteLine("Event Queue Monitor: Type: " + evtype +
                ", Size: " + evq_size +
                ", Delay: " + evq_delay + " usecs.");
        }
    }

    class UMERegistrationIdCompat10
    {
        private long _regid_offset;

        public UMERegistrationIdCompat10(String regid_offset)
        {
            try
            {
                _regid_offset = Convert.ToInt64(regid_offset);
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine("Can't convert registration ID offset to a long: " + ex.ToString());
                System.Environment.Exit(1);
            }
        }

        public long setRegistrationId(object cbArg, string source, long sourceRegId)
        {
            long regid = (_regid_offset == 0 ? 0 : sourceRegId + _regid_offset);
            if (regid < 0)
            {
                System.Console.Out.WriteLine("Would have requested registration ID [" + regid + "], but negative registration IDs are invalid.");
                regid = 0;
            }
            System.Console.Out.WriteLine("Requesting registration ID [" + regid + "], Source: " + source + "/" + sourceRegId);
            return regid;
        }
    }

    class UMERegistrationId
    {
        private long _regid_offset;

        public UMERegistrationId(String regid_offset)
        {
            try
            {
                _regid_offset = Convert.ToInt64(regid_offset);
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine("Can't convert registration ID offset to a long: " + ex.ToString());
                System.Environment.Exit(1);
            }
        }

        public uint setRegistrationId(object cbArg, UMERegistrationIdExCallbackInfo cbInfo)
        {
            long regid = (_regid_offset == 0 ? 0 : cbInfo.sourceRegistrationId() + _regid_offset);
            if (regid < 0)
            {
                System.Console.Out.WriteLine("Would have requested registration ID [" + regid + "], but negative registration IDs are invalid.");
                regid = 0;
            }
            System.Console.Out.WriteLine("Store " + cbInfo.storeIndex() + ": " + cbInfo.store() + "["
                + cbInfo.source() + "][" + cbInfo.sourceRegistrationId() + "] Flags " + cbInfo.flags()
                + ". Requesting regid: " + regid);
            return (uint)regid;
        }
    }

    class UMERcvRecInfo
    {
        private int _seqnum_offset = 0;

        public UMERcvRecInfo(int seqnum_offset)
        {
            _seqnum_offset = seqnum_offset;
        }

        public int setRecoverySequenceNumberInfo(object cbArg, UMERecoverySequenceNumberCallbackInfo cbInfo)
        {
            long new_low = cbInfo.lowSequenceNumber() + _seqnum_offset;
            if (new_low < 0)
            {
                System.Console.Out.WriteLine("New low sequence number would be negative.  Leaving low SQN unchanged.");
                new_low = cbInfo.lowSequenceNumber();
            }
            System.Console.Out.WriteLine("SQNs Low " + cbInfo.lowSequenceNumber() + " (will set to "
                + new_low + "), Low rxreqmax " + cbInfo.lowRxReqMaxSequenceNumber()
                + ", High " + cbInfo.highSequenceNumber());

            cbInfo.setLowSequenceNumber((uint)new_low);
			System.Console.Out.Flush();
            return 0;
        }
    }

    class UMERcvReceiver
    {
        public long imsg_count = 0;
        public long msg_count = 0;
        public long total_msg_count = 0;
        public long subtotal_msg_count = 0;
        public long byte_count = 0;
        public long unrec_count = 0;
        public long total_unrec_count = 0;
        public long burst_loss = 0;
        public long rx_msgs = 0;
        public long otr_msgs = 0;

        public long data_start_time = 0;
        public long data_end_time = 0;

        public int stotal_msg_count = 0;
        public int _dereg = 0;
        public long total_byte_count = 0;

        private bool _verbose = false;
        private bool _end_on_eos = false;
        private bool _summary = false;
        private int _exack = 0;
		private LBMReceiver _lbmrcvr;

        public UMERcvReceiver(bool verbose, bool end_on_eos, bool summary, int exack, int dereg)
        {
            _verbose = verbose;
            _end_on_eos = end_on_eos;
            _summary = summary;
            _exack = exack;
            _dereg = dereg;
        }
		public void setRcvr(LBMReceiver rcvr)
		{
			_lbmrcvr = rcvr;
		}

        // This immediate-mode receiver is *only* used for topicless
        // immediate-mode sends.  Immediate sends that use a topic
        // are received with normal receiver objects.
        public int onReceiveImmediate(object cbArg, LBMMessage msg)
        {
            imsg_count++;
            return onReceive(cbArg, msg);
        }

        public int onReceive(object cbArg, LBMMessage msg)
        {
            switch (msg.type())
            {
                case LBM.MSG_DATA:
                    if (stotal_msg_count == 0)
                        data_start_time = Environment.TickCount;
                    else
                        data_end_time = Environment.TickCount;
                    msg_count++;
                    total_msg_count++;
					if ((total_msg_count == 100) && _dereg == 1)
					{
            			System.Console.Out.WriteLine("Sending DEREGISTRATION\n");
						System.Console.Out.Flush();
						_lbmrcvr.umederegister();
						

					}
                    stotal_msg_count++;
                    subtotal_msg_count++;
                    byte_count += msg.data().Length;
                    total_byte_count += msg.data().Length;

                    if ((msg.flags() & LBM.MSG_FLAG_RETRANSMIT) != 0)
                    {
                        rx_msgs++;
                    }
                    if ((msg.flags() & LBM.MSG_FLAG_OTR) != 0)
                    {
                        otr_msgs++;
                    }

                    if (_verbose)
                    {
                        System.Console.Error.Write("["
                            + msg.topicName()
                            + "]["
                            + msg.source()
                            + "]["
                            + msg.sequenceNumber()
                            + "]");
                        if ((msg.flags() & LBM.MSG_FLAG_UME_RETRANSMIT) != 0)
                        {
                            System.Console.Error.Write("-RX-");
                        }
                        if ((msg.flags() & LBM.MSG_FLAG_OTR) != 0)
                        {
                            System.Console.Error.Write("-OTR-");
                        }
                        System.Console.Error.Write(", ");
                        System.Console.Error.WriteLine(msg.data().Length
                                                                + " bytes");
                    }
                    if (_exack > 0)
                    {
                        if ((msg.sequenceNumber() % _exack) == 0)
                        {
                            if (_verbose)
                                System.Console.Out.WriteLine(" Sending Explicit ACK");
                            try
                            {
                                msg.sendExplicitAck();
                            }
                            catch (Exception e)
                            {
                                System.Console.Error.WriteLine("msg.sendExplicitAck(): " + e.Message);
                            }
                        }
                    }
                    break;
                case LBM.MSG_BOS:
                    System.Console.Error.WriteLine("[" + msg.topicName() + "][" + msg.source() + "], Beginning of Transport Session");
                    break;
                case LBM.MSG_EOS:
                    System.Console.Error.WriteLine("[" + msg.topicName() + "][" + msg.source() + "], End of Transport Session");
                    if (_end_on_eos)
                    {
                        if (_summary)
                            print_summary();
                        end();
                    }
                    subtotal_msg_count = 0;
                    break;
                case LBM.MSG_UNRECOVERABLE_LOSS:
                    unrec_count++;
                    total_unrec_count++;
                    if (_verbose)
                        System.Console.Error.WriteLine("[" + msg.topicName() + "][" + msg.source() + "][" + msg.sequenceNumber().ToString("x") + "], LOST");
                    break;
                case LBM.MSG_UNRECOVERABLE_LOSS_BURST:
                    burst_loss++;
                    if (_verbose)
                        System.Console.Error.WriteLine("[" + msg.topicName() + "][" + msg.source() + "], LOST BURST");
                    break;
                case LBM.MSG_REQUEST:
                    if (stotal_msg_count == 0)
                        data_start_time = Environment.TickCount;
                    else
                        data_end_time = Environment.TickCount;
                    msg_count++;
                    stotal_msg_count++;
                    subtotal_msg_count++;
                    byte_count += msg.data().Length;
                    total_byte_count += msg.data().Length;
                    break;
                case LBM.MSG_UME_REGISTRATION_ERROR:
                    System.Console.Error.WriteLine("[" + msg.topicName() + "][" + msg.source() + "] UME registration error: " + msg.dataString());
                    break;
                case LBM.MSG_UME_REGISTRATION_SUCCESS:
                    System.Console.Error.WriteLine("[" + msg.topicName() + "][" + msg.source() + "] UME registration successful. Src RegID " + msg.sourceRegistrationId() + " RegID " + msg.receiverRegistrationId());
                    break;
                case LBM.MSG_UME_DEREGISTRATION_SUCCESS_EX:
					System.Console.Out.Write("Received MSG_UME_DEREGISTRATION_SUCCESS_EX\n");
                    UMEDeregistrationSuccessInfo dereg = msg.deregistrationSuccessInfo();
                    System.Console.Out.Write("[" + msg.topicName() + "][" + msg.source()
                            + "] store " + dereg.storeIndex() + ": "
                            + dereg.store() + " UME registration successful. SrcRegID "
                            + dereg.sourceRegistrationId() + " RcvRegID " + dereg.receiverRegistrationId()
                            + ". Flags " + dereg.flags() + " ");
                    if ((dereg.flags() & LBM.MSG_UME_REGISTRATION_SUCCESS_EX_FLAG_OLD) != 0)
                        System.Console.Out.Write("OLD[SQN " + dereg.sequenceNumber() + "] ");
                    if ((dereg.flags() & LBM.MSG_UME_REGISTRATION_SUCCESS_EX_FLAG_NOCACHE) != 0)
                        System.Console.Out.Write("NOCACHE ");
                    System.Console.Out.WriteLine();
					break;
                case LBM.MSG_UME_DEREGISTRATION_COMPLETE_EX:
					System.Console.Out.Write("Received MSG_UME_DEREGISTRATION_COMPLETE_EX\n");
					break;
					
                case LBM.MSG_UME_REGISTRATION_SUCCESS_EX:
                    UMERegistrationSuccessInfo reg = msg.registrationSuccessInfo();
                    System.Console.Out.Write("[" + msg.topicName() + "][" + msg.source()
                            + "] store " + reg.storeIndex() + ": "
                            + reg.store() + " UME registration successful. SrcRegID "
                            + reg.sourceRegistrationId() + " RcvRegID " + reg.receiverRegistrationId()
                            + ". Flags " + reg.flags() + " ");
                    if ((reg.flags() & LBM.MSG_UME_REGISTRATION_SUCCESS_EX_FLAG_OLD) != 0)
                        System.Console.Out.Write("OLD[SQN " + reg.sequenceNumber() + "] ");
                    if ((reg.flags() & LBM.MSG_UME_REGISTRATION_SUCCESS_EX_FLAG_NOCACHE) != 0)
                        System.Console.Out.Write("NOCACHE ");
                    System.Console.Out.WriteLine();
                    break;
                case LBM.MSG_UME_REGISTRATION_COMPLETE_EX:
                    UMERegistrationCompleteInfo regcomplete = msg.registrationCompleteInfo();
                    System.Console.Out.Write("[" + msg.topicName() + "][" + msg.source()
                            + "] UME registration complete. SQN " + regcomplete.sequenceNumber()
                            + ". Flags " + regcomplete.flags() + " ");
                    if ((regcomplete.flags() & LBM.MSG_UME_REGISTRATION_COMPLETE_EX_FLAG_QUORUM) != 0)
                    {
                        System.Console.Out.Write("QUORUM ");
                    }
                    if ((regcomplete.flags() & LBM.MSG_UME_REGISTRATION_COMPLETE_EX_FLAG_RXREQMAX) != 0)
                    {
                        System.Console.Out.Write("RXREQMAX ");
                    }
                    System.Console.Out.WriteLine();
                    break;
                case LBM.MSG_UME_REGISTRATION_CHANGE:
                    System.Console.Error.WriteLine("[" + msg.topicName() + "][" + msg.source() + "] UME registration change: " + msg.dataString());
                    break;
                default:
                    System.Console.Error.WriteLine("Unknown lbm_msg_t type " + msg.type() + " [" + msg.topicName() + "][" + msg.source() + "]");
                    break;
            }
            msg.dispose();		// Send ACK now
			System.Console.Out.Flush();
            return 0;
        }

        private void print_summary()
        {
            double total_time_sec, mps, bps;

            total_time_sec = 0.0;
            mps = 0.0;
            bps = 0.0;

            long bits_received = total_byte_count * 8;
            long total_time = Math.Abs(data_end_time - data_start_time);

            total_time_sec = total_time / 1000.0;

            if (total_time_sec > 0)
            {
                mps = stotal_msg_count / total_time;
                bps = bits_received / total_time / 1000.0;
            }

            System.Console.Out.WriteLine("\nTotal time         : "
                       + total_time_sec.ToString("0.000")
                       + "  sec");
            System.Console.Out.WriteLine("Messages received  : "
                       + stotal_msg_count);
            System.Console.Out.WriteLine("Bytes received     : "
                       + total_byte_count);
            System.Console.Out.WriteLine("Avg. throughput    : "
                       + mps.ToString("0.000")
                       + " Kmsgs/sec, "
                       + bps.ToString("0.000")
                       + " Mbps\n\n");
			
        }

        private void end()
        {
            System.Console.Out.WriteLine("Quitting.... received "
                + total_msg_count
                + " messages");
            System.Environment.Exit(0);
        }
    }

}