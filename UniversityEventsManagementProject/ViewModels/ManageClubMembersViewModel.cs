using System.Collections.Generic;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.ViewModels
{
    public class ManageClubMembersViewModel
    {
        public List<ClubMember> PendingMembers { get; set; } = new List<ClubMember>();
        public List<Club> Clubs { get; set; } = new List<Club>();
    }
}

