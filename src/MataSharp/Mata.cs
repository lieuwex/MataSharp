﻿//(c) 2014 Lieuwe Rooijakkers
//MataSharp; Public C# implementation of the non public 'Mata' API.
using System;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MataSharp
{
    internal static class _Session 
    {
        public static Mata Mata; 
        public static MagisterSchool School; 
        public static MataHTTPClient HttpClient = new MataHTTPClient(); 
    }

    /// <summary>
    /// Type to communicate with a Magister School's server.
    /// </summary>
    public partial class Mata : IDisposable
    {
        public string Name { get; internal set; }
        public uint UserID { get; internal set; }
        public string SessionID { get; internal set; }

        public string UserName { get; internal set; }
        internal MagisterSchool School { get; private set; }
        public MagisterPerson Person { get; internal set; }

        /// <summary>
        /// <para>Creates 'Mata' instance to communicate with the Mata server of the specified school.</para>
        /// </summary>
        /// <param name="School">School which to log in to.</param>
        /// <param name="UserName">Name to log in with.</param>
        /// <param name="UserPassword">Password to log in with.</param>
        public Mata(MagisterSchool School, string UserName, string UserPassword)
        {
            this.School = School;
            this.UserName = UserName;

            string url = "https://" + School.URL + "/api/sessie";
            string response = _Session.HttpClient.Post(url, new NameValueCollection()
                {
                    {"Gebruikersnaam", UserName},
                    {"Wachtwoord", UserPassword}
                });
            var cleanResponse = JsonConvert.DeserializeObject<MagisterStyleMata>(response);
            this.Name = cleanResponse.Naam;
            this.UserID = uint.Parse(cleanResponse.GebruikersId);
            this.SessionID = cleanResponse.SessieId;

            _Session.Mata = this;
            _Session.School = this.School;

            _Session.HttpClient.client.Headers[HttpRequestHeader.Cookie] = "SESSION_ID=" + this.SessionID + "&fileDownload=true"; //yummy! cookies!

            this.Person = this.GetPersons(this.Name)[0]; //Get itself as MagisterPerson from the servers.
            _Session.Mata = this;
        }

        /// <summary>
        /// <para>Get all messagefolders linked with the current Mata instance.</para>
        /// </summary>
        public List<MagisterMessageFolder> GetMessageFolders()
        {
            string url = "https://" + this.School.URL + "/api/personen/" + this.UserID + "/communicatie/berichten/mappen?$skip=0&$top=50";

            string MessageFoldersRAW = _Session.HttpClient.client.DownloadString(url);
            var MessageFolders = JArray.Parse(MessageFoldersRAW).ToArray();

            List<MagisterMessageFolder> tmplst = new List<MagisterMessageFolder>();
            foreach (var MessageFolder in MessageFolders)
            {
                var tmp = JsonConvert.DeserializeObject<MagisterStyleMessageFolderListItem>(MessageFolder.ToString());
                var tmpType = (tmp.Id == -101) ? MessageFolderType.Inbox : (tmp.Id == -102) ? MessageFolderType.Bin : MessageFolderType.SentMessages;

                tmplst.Add(new MagisterMessageFolder()
                {
                    Name = tmp.Naam,
                    UnreadMessagesCount = tmp.OngelezenBerichten,
                    ID = tmp.Id,
                    ParentID = tmp.ParentId,
                    Ref = tmp.Ref,
                    MessagesURI = tmp.BerichtenUri,
                    School = this.School,
                    Mata = this,
                    FolderType = tmpType
                });
            }
            return tmplst;
        }

        /// <summary>
        /// Returns all Magisterpersons filterd by the given search filter as a list.
        /// </summary>
        /// <param name="SearchFilter">The search filter to use as string.</param>
        /// <returns>List containing MagisterPerson instances</returns>
        public List<MagisterPerson> GetPersons(string SearchFilter)
        {
            if (string.IsNullOrWhiteSpace(SearchFilter) || SearchFilter.Count() < 3) return new List<MagisterPerson>();

            string URL = "https://" + this.School.URL + "/api/personen/" + this.UserID + "/communicatie/contactpersonen?q=" + SearchFilter;

            string personsRAW = _Session.HttpClient.client.DownloadString(URL);
            return JArray.Parse(personsRAW).ToList().ConvertAll(p => p.ToObject<MagisterStylePerson>().ToPerson());
        }

        public List<Homework> GetHomework()
        {
            string URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/huiswerk/huiswerk";

            string homeworkListRaw = _Session.HttpClient.client.DownloadString(URL);
            var homeworkListClean = JsonConvert.DeserializeObject<HuiswerkLijst>(homeworkListRaw);

            var compactHomeworkItemDays = homeworkListClean.Items.Select(x=>x.Items);

            var tmpList = new List<Homework>();

            foreach (var compactHomeworkItemDay in compactHomeworkItemDays)
            {
                foreach (var compactHomeworkItem in compactHomeworkItemDay)
                {
                    URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/huiswerk/huiswerk/" + compactHomeworkItem.Id;
                    string homeworkItemRaw = _Session.HttpClient.client.DownloadString(URL);
                    var tmpHomework = JsonConvert.DeserializeObject<Huiswerk>(homeworkItemRaw).ToHomework();
                    tmpHomework.ClassAbbreviation = compactHomeworkItem.VakAfkortingen; //Full homework from the server doesn't contain the classAbbreviations.
                    tmpList.Add(tmpHomework);
                }
            }
            return tmpList;
        }

        public List<Homework> GetTasks()
        {
            string URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/huiswerk/taken";

            string homeworkListRaw = _Session.HttpClient.client.DownloadString(URL);
            var homeworkListClean = JsonConvert.DeserializeObject<HuiswerkLijst>(homeworkListRaw);

            var compactIDs_Days = homeworkListClean.Items.Select(x => x.Items.Select(y => y.Id));

            var tmpList = new List<Homework>();

            foreach (var compactIDday in compactIDs_Days)
            {
                foreach (int compactID in compactIDday)
                {
                    URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/huiswerk/huiswerk/" + compactID;
                    string homeworkItemRaw = _Session.HttpClient.client.DownloadString(URL);
                    tmpList.Add(JsonConvert.DeserializeObject<Huiswerk>(homeworkItemRaw).ToHomework());
                }
            }
            return tmpList;
        }

        public List<Homework> GetTests()
        {
            string URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/huiswerk/toetsen";

            string homeworkListRaw = _Session.HttpClient.client.DownloadString(URL);
            var homeworkListClean = JsonConvert.DeserializeObject<HuiswerkLijst>(homeworkListRaw);

            var compactIDs_Days = homeworkListClean.Items.Select(x => x.Items.Select(y => y.Id));

            var tmpList = new List<Homework>();

            foreach (var compactIDday in compactIDs_Days)
            {
                foreach (int compactID in compactIDday)
                {
                    URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/huiswerk/huiswerk/" + compactID;
                    string homeworkItemRaw = _Session.HttpClient.client.DownloadString(URL);
                    tmpList.Add(JsonConvert.DeserializeObject<Huiswerk>(homeworkItemRaw).ToHomework());
                }
            }
            return tmpList;
        }

        public List<StudyGuide> GetStudyGuides()
        {
            string URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/studiewijzers?$skip=0&$top=50";

            string compactStudyGuidesRaw = _Session.HttpClient.client.DownloadString(URL);
            var compactStudyGuidesClean = JsonConvert.DeserializeObject<StudieWijzerLijst>(compactStudyGuidesRaw).Items;

            var list = new List<StudyGuide>();
            foreach(var compactStudyGuide in compactStudyGuidesClean)
            {
                URL = "https://" + School.URL + "/api/leerlingen/" + this.UserID + "/studiewijzers/" + compactStudyGuide.Id;

                string studyGuideRaw = _Session.HttpClient.client.DownloadString(URL);
                var studyGuideClean = JsonConvert.DeserializeObject<StudieWijzer>(studyGuideRaw);

                list.Add(studyGuideClean.ToStudyGuide());
            }
            return list;
        }

        public List<Assignment> GetAssignments()
        {
            string URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/opdrachten/status/openstaand?$skip=0&$top=30";

            string CompactAssignmentsRaw = _Session.HttpClient.client.DownloadString(URL);
            var CompactAssignments = JsonConvert.DeserializeObject<AssignmentFolder>(CompactAssignmentsRaw);

            List<Assignment> list = new List<Assignment>();
            foreach (var CompactAssignment in CompactAssignments.Items.ToList().ConvertAll(m => JsonConvert.DeserializeObject<AssignmentFolderListItem>(m.ToString())))
            {
                URL = "https://" + School.URL + "/api/leerlingen/" + this.UserID + "/opdrachten/" + CompactAssignment.Id;
                string AssignmentRaw = _Session.HttpClient.client.DownloadString(URL);
                var AssignmentClean = JsonConvert.DeserializeObject<AssignmentFolderItem>(AssignmentRaw);
                list.Add(AssignmentClean.toAssignment());
            }
            return list;
        }

        public List<DigitalSchoolUtility> GetDigitalSchoolUtilities()
        {
            string URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/digitaallesmateriaal/vakken";

            string compactUtilitiesRaw = _Session.HttpClient.client.DownloadString(URL);
            var compactUtilities = JArray.Parse(compactUtilitiesRaw).ToList().ConvertAll(u => u.ToObject<DigitaalLesMatriaalLijstItem>());

            var tmpList = new List<DigitalSchoolUtility>();
            foreach(var compactUtility in compactUtilities)
            {
                URL = "https://" + this.School.URL + "/api/leerlingen/" + this.UserID + "/digitaallesmateriaal/vakken/" + compactUtility.Id;

                string utilityRaw = _Session.HttpClient.client.DownloadString(URL);
                var utility = JsonConvert.DeserializeObject<DigitaalLesMatriaal[]>(utilityRaw)[0];
                utility.Id = compactUtility.Id; //Fix for the 'small' problem in the Mata API.
                tmpList.Add(utility.ToDigitalSchoolUtility());
            }
            return tmpList;
        }

        public MagisterMessageFolder Inbox
        {
            get { return this.GetMessageFolders().Where(mf => mf.FolderType == MessageFolderType.Inbox).ElementAt(0); }
        }

        public MagisterMessageFolder SentMessages
        {
            get { return this.GetMessageFolders().Where(mf => mf.FolderType == MessageFolderType.SentMessages).ElementAt(0); }
        }

        public MagisterMessageFolder Bin
        {
            get { return this.GetMessageFolders().Where(mf => mf.FolderType == MessageFolderType.Bin).ElementAt(0); }
        }

        ~Mata() { this.Dispose(); }
        public void Dispose() { _Session.HttpClient.Dispose(); GC.Collect(); }
    }

    internal partial struct MagisterStyleMata
    {
        public string Naam { get; set; }
        public string GebruikersId { get; set; }
        public string SessieId { get; set; }
        public string Message { get; set; }
        public string State { get; set; }
    }
}
