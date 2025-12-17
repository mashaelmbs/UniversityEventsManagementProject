using System.Collections.Generic;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.ViewModels
{
    public class ViewClubMembersViewModel
    {
        public Club Club { get; set; }
        public List<ClubMember> PendingMembers { get; set; } = new List<ClubMember>();
        public List<ClubMember> ApprovedMembers { get; set; } = new List<ClubMember>();
        public List<ClubMember> RejectedMembers { get; set; } = new List<ClubMember>();
    }
}

