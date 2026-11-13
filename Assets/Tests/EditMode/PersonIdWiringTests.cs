using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	/// <summary>Person ids saved in the demo scene are distinct, and prefab assets carry none.</summary>
	public class PersonIdWiringTests
	{
		// A character saved directly ("personId: abc") or as a prefab instance override
		// ("propertyPath: personId" then "value: abc").
		static readonly Regex Direct = new Regex(@"^\s*personId: (\S+)\s*$", RegexOptions.Multiline);
		static readonly Regex Override = new Regex(@"propertyPath: personId\s*\r?\n\s*value: (\S*)", RegexOptions.Multiline);

		[Test]
		public void Test1_PersonIdsDistinct()
		{
			string text = File.ReadAllText("Assets/Scenes/test1.unity");
			List<string> ids = Direct.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value)
				.Concat(Override.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value))
				.Where(v => !string.IsNullOrEmpty(v))
				.ToList();
			List<string> duplicates = ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
			Assert.IsEmpty(duplicates, "duplicate person ids in test1: " + string.Join(", ", duplicates));
		}

		[Test]
		public void NpcPrefabs_HaveNoPersonId()
		{
			foreach (string path in Directory.GetFiles("Assets/Prefabs/NPC", "*.prefab", SearchOption.AllDirectories))
			{
				string text = File.ReadAllText(path);
				foreach (Match m in Direct.Matches(text))
					Assert.Fail($"{path} has its own person id {m.Groups[1].Value}; every instance would share it.");
			}
		}
	}
}
