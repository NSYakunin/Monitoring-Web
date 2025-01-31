namespace web_test
{
    public class WorkItem
    {
        public string? DocumentName { get; set; }        // td.Name + ' ' + d.Name
        public string? WorkName { get; set; }            // w.Name
        public string? Executor { get; set; }            // u.smallName
        public string? Controller { get; set; }          // (SELECT smallName FROM Users WHERE idUser = wucontr.idUser)
        public string? Approver { get; set; }            // (SELECT smallName FROM Users WHERE idUser = wuc.idUser)
        public DateTime? PlanDate { get; set; }         // w.DatePlan
        public DateTime? Korrect1 { get; set; }         // wu.DateKorrect1
        public DateTime? Korrect2 { get; set; }         // wu.DateKorrect2
        public DateTime? Korrect3 { get; set; }         // wu.DateKorrect3
        public DateTime? FactDate { get; set; }         // w.DateFact
    }
}
