﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sdl.LanguagePlatform.Core;
using Sdl.LanguagePlatform.TranslationMemory;
using TMX_Lib.Db;
using TMX_Lib.Search;
using TMX_Lib.TmxFormat;
using TMX_Lib.Utils;
using TMX_Lib.XmlSplit;
using LogManager = Sdl.LanguagePlatform.TranslationMemory.LogManager;

namespace QuickTmxTesting
{
	// quick and dirty tests
    internal class Program
    {
	    private static readonly Logger log = NLog.LogManager.GetCurrentClassLogger();

	    private static async Task TestImportFile(string file, string dbName, bool quickImport = false)
	    {
		    var db = new TmxMongoDb("localhost:27017", dbName);
		    await db.ImportToDbAsync(file, quickImport);
	    }

		// performs the database fuzzy-search, not our Fuzzy-search (our fuzzy search is more constraining)
		private static async Task TestDatabaseFuzzySimple4(string root)
		{
			var db = new TmxMongoDb("localhost:27017", "sample4");
			await db.ImportToDbAsync($"{root}\\SampleTestFiles\\#4.tmx");
			var search = new TmxSearch(db);
			await search.LoadLanguagesAsync();

			var result = await db.FuzzySearch("abc def", "en-GB", "es-MX");
			Debug.Assert(result.Count == 0);

			result = await db.FuzzySearch("This document contains both the Interserve Construction Health and Safety Code for Subcontractors and the Sustainability Code for Subcontractors.", "en-GB", "es-MX");
			Debug.WriteLine($"best score:{result[0].Score}");
			result = await db.FuzzySearch("En el cumplimiento de estas metas, lograr nuestra visión de ser el socio de confianza para todos aquellos con quienes tenemos una relación, accionistas, clientes, empleados, proveedores, miembros de la comunidad en la que estamos trabajando, o de cualquier otro grupo o persona.", "es-MX", "en-GB");
			Debug.WriteLine($"best score:{result[0].Score}");

			result = await db.FuzzySearch("This document Interserve Health Safety", "en-GB", "es-MX");
			Debug.Assert(result.Count >= 3 && result.Any(r => r.TargetText == "Este documento contiene el Interserve Construcción Código de Salud y Seguridad de los subcontratistas y la sostenibilidad Código de los subcontratistas.") );

			result = await db.FuzzySearch("construction subcontractors", "en-GB", "es-MX");
			Debug.Assert(result.Count >= 3 
			&& result.Any(r => r.SourceText == "This document contains both the Interserve Construction Health and Safety Code for Subcontractors and the Sustainability Code for Subcontractors.")
			&& result.Any(r => r.SourceText == "These codes have been prepared to ensure that Interserve Construction and all subcontractors on Interserve Construction contracts operate to clear and consistent standards and in doing so assist us in meeting our goals of being accident free and reducing our environmental impact.")
			&& result.Any(r => r.SourceText == "Subcontractors are required to assist and co-operate with Interserve Construction with health, safety and environmental related issues, including initiatives that may be operated from time to time.")
			&& result.All(r => r.SourceText != "In meeting these goals we will achieve our vision of being the trusted Partner to all those with whom we have a relationship be they shareholders, customers, employees, suppliers, members of the community in which we are working, or any other group or individual.")
			);

			result = await db.FuzzySearch("construction health", "en-GB", "es-MX");
			Debug.Assert(result.Count >= 2
			             && result.Any(r => r.SourceText == "This document contains both the Interserve Construction Health and Safety Code for Subcontractors and the Sustainability Code for Subcontractors.")
			             && result.Any(r => r.SourceText == "Subcontractors are required to assist and co-operate with Interserve Construction with health, safety and environmental related issues, including initiatives that may be operated from time to time.")
			             && result.All(r => r.SourceText != "In meeting these goals we will achieve our vision of being the trusted Partner to all those with whom we have a relationship be they shareholders, customers, employees, suppliers, members of the community in which we are working, or any other group or individual.")
			);

			result = await db.FuzzySearch("construcción salud", "es-MX", "en-GB");
			Debug.Assert(result.Count >= 2
			             && result.Any(r => r.TargetText== "This document contains both the Interserve Construction Health and Safety Code for Subcontractors and the Sustainability Code for Subcontractors.")
			             && result.Any(r => r.TargetText == "Subcontractors are required to assist and co-operate with Interserve Construction with health, safety and environmental related issues, including initiatives that may be operated from time to time.")
			             && result.All(r => r.TargetText != "In meeting these goals we will achieve our vision of being the trusted Partner to all those with whom we have a relationship be they shareholders, customers, employees, suppliers, members of the community in which we are working, or any other group or individual.")
			);
		}

		private static Segment TextToSegment(string text)
		{
			var s = new Segment();
			s.Add(text);
			return s;
		}

		private static async Task TestFuzzySimple4(string root)
		{
			var db = new TmxMongoDb("localhost:27017", "sample4");
			await db.ImportToDbAsync($"{root}\\SampleTestFiles\\#4.tmx");
			var search = new TmxSearch(db);
			await search.LoadLanguagesAsync();

			var fuzzy = TmxSearchSettings.Default();
			fuzzy.Mode = SearchMode.FuzzySearch;
			var en_sp = new LanguagePair("en-GB", "es-MX");
			var sp_en = new LanguagePair("es-MX", "en-GB");
			var results = await search.Search(fuzzy, TextToSegment("This document contains both the Interserve Construction Health and Safety Code for Subcontractors and the Sustainability Code"), en_sp);
			results = await search.Search(fuzzy, TextToSegment("This document contains both Interserve Construction Health Safety Code for Subcontractors and Sustainability Code"), en_sp);
			results = await search.Search(fuzzy, TextToSegment("This document contains both Interserve Construction Safety Code for and Code"), en_sp);
		}

		public enum SearchType
		{
			Exact, Fuzzy, Concordance,
		}

		private static async Task TestSimpleSearch(string dbName, string text, SearchType searchType, string sourceLanguage, string targetLanguage)
		{
			var db = new TmxMongoDb("localhost:27017", dbName);
			var search = new TmxSearch(db);
			await search.LoadLanguagesAsync();

			log.Debug($"search [{text}] - started");
			var watch = Stopwatch.StartNew();
			IReadOnlyList<TmxSegment> result;
			switch (searchType)
			{
				case SearchType.Exact:
					result = await db.ExactSearch(text, sourceLanguage, targetLanguage);
					break;
				case SearchType.Fuzzy:
					result = await db.FuzzySearch(text, sourceLanguage, targetLanguage);
					break;
				case SearchType.Concordance:
					result = await db.ConcordanceSearch(text, sourceLanguage, targetLanguage);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(searchType), searchType, null);
			}
			log.Debug($"search [{text}] - {result.Count} results - took {watch.ElapsedMilliseconds} ms");
		}


		private static void SplitLargeXmlFile(string inputXmlFile, string outputPrefix)
		{
			Directory.CreateDirectory(outputPrefix);
		    var splitter = new XmlSplitter(inputXmlFile);
		    var idx = 0;
		    while (true)
		    {
			    var str = splitter.TryGetNextString();
			    if (str == null)
				    return;
			    var outFile = $"{outputPrefix}{++idx:D3}.xml";
				File.WriteAllText(outFile, str);
		    }
	    }

		private static bool ContainsSource(SearchResults sr, string text)
		{
			return sr.Results.Any(r => r.TranslationProposal.SourceSegment.ToPlain() == text);
		}
		private static bool ContainsTarget(SearchResults sr, string text)
		{
			return sr.Results.Any(r => r.TranslationProposal.TargetSegment.ToPlain() == text);
		}

		private static async Task<bool> ExpectInSearch(string source, string target, TmxSearch search, string sourceLanguage, string targetLanguage)
		{
			var result = await search.Search(TmxSearchSettings.Default(), TextToSegment(source), new LanguagePair(sourceLanguage, targetLanguage));
			return (ContainsTarget(result, target));
		}

		private static async Task TestEnRoImport()
		{
			// run it after:
			// Task.Run(() => TestImportFile("C:\\john\\buff\\TMX Examples\\TMX Test Files\\large2\\en-ro.tmx", "en-ro", quickImport: true)).Wait();
			var db = new TmxMongoDb("localhost:27017", "en-ro");
			await db.InitAsync();
			var search = new TmxSearch(db);
			await search.LoadLanguagesAsync();

			Debug.Assert(await ExpectInSearch(  "We might call them the words of \"unforgiveness.\"", 
												"Le putem numi cuvintele „ne-iertării”.", 
												search, "en-GB", "ro-RO"));
			Debug.Assert(await ExpectInSearch("When in doubt, tell the truth.",
				"„Când ai dubii, spune adevărul.”",
				search, "en-GB", "ro-RO"));
			Debug.Assert(await ExpectInSearch("Yes, even between the land and the ship.”",
				"Da, chiar şi între pământ şi navă\".",
				search, "en-GB", "ro-RO"));
			Debug.Assert(await ExpectInSearch("The Scriptures show that the first stage of our Lord's parousia, presence, will be secret",
				"Scripturile arată că prima etapă a parousiei sau prezenţei Domnului nostru va fi secretă.",
				search, "en-GB", "ro-RO"));
			Debug.Assert(await ExpectInSearch("I know the Three Kings do not exist but I give you this great present",
				"Stiu ca cei trei regi nu exista, dar iti ofer acest mare cadou.”",
				search, "en-GB", "ro-RO"));

			Debug.Assert(await ExpectInSearch("Stiu ca cei trei regi nu exista, dar iti ofer acest mare cadou",
				"I know the Three Kings do not exist but I give you this great present.”",
				search, "ro-ro", "en-US"));
			Debug.Assert(await ExpectInSearch("Ați fost învățați de cunoaștere că fericirea oamenilor depinde de ceea ce au creat cu propriile lor mâini",
				"Were you taught by knowledge that people’s happiness depended on what they created with their own hands?",
				search, "ro-ro", "en-US"));
			Debug.Assert(await ExpectInSearch("În trecut nu prea exista ocazie ca poporul Domnului să vegheze la împlinirea Scripturii; pentru că aceste împliniri erau departe unele de altele",
				"In the past there was little opportunity for the Lord's people to watch the fulfilments of Scripture; for these fulfilments were far apart.",
				search, "ro-ro", "en-US"));
		}

		static void Main(string[] args)
		{
			LogUtil.Setup();
			//SplitLargeXmlFile("C:\\john\\buff\\TMX Examples\\TMX Test Files\\large\\en(GB) - it(IT)_(DGT 2015, 2017).tmx", "C:\\john\\buff\\TMX Examples\\temp\\");
			//SplitLargeXmlFile("C:\\john\\buff\\TMX Examples\\TMX Test Files\\large\\en-fr (EU Bookshop v2_10.8M).tmx", "C:\\john\\buff\\TMX Examples\\temp2\\");
			log.Debug("test started");
			//SplitLargeXmlFile("C:\\john\\buff\\TMX Examples\\TMX Test Files\\fails\\opensubtitlingformat.tmx", "C:\\john\\buff\\TMX Examples\\temp3\\");


			Task.Run(() => TestImportFile("C:\\john\\buff\\TMX Examples\\TMX Test Files\\large2\\en-ro.tmx", "en-ro-2", quickImport: true)).Wait();

			//TestEnRoImport().Wait();
			//TestSimpleSearch("sample4", "introduction", SearchType.Exact, "en-gb", "es-mx").Wait();
			//TestSimpleSearch("en-frEUBookshopv2108M", "The European Social Fund in French Guiana", SearchType.Exact, "en", "fr").Wait();
			//TestSimpleSearch("en-ro", "We might call them the words of \"unforgiveness", SearchType.Exact, "en", "ro").Wait();
			return;

			var root = ".";
	        if (args.Length > 0)
		        root = args[0];

	        Task.Run(() => TestImportFile("C:\\john\\buff\\TMX Examples\\TMX Test Files\\large2\\en-ro.tmx", "en-ro", quickImport: true)).Wait();

			//Task.Run(() => TestImportSmallFile2(root)).Wait();
			//Task.Run(() => TestImportSample4(root)).Wait();
			//Task.Run(() => TestDatabaseFuzzySimple4(root)).Wait();

			//Task.Run(() => TestFuzzySimple4(root)).Wait();

			log.Debug("test complete");
	        Console.ReadLine();
        }
    }
}
