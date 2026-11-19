using StraussDrive.Import;
var root = args.FirstOrDefault() ?? HackathonImporter.FindRoot(Directory.GetCurrentDirectory());
var report = HackathonImporter.Load(root).ValidateReferences();
Console.WriteLine($"Original fictional references: {report.Observations.Length - report.Mismatches.Length} matched, {report.Mismatches.Length} mismatched.");
foreach (var item in report.Mismatches) Console.WriteLine($"{item.EmployeeId}: {item.Field}, expected {item.Expected}, actual {item.Actual}");
Environment.ExitCode = report.Mismatches.Length == 0 ? 0 : 1;
