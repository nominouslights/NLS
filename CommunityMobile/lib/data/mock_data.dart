import 'package:flutter/material.dart';

/// All hardcoded mockup data, transcribed from the design board.
/// Mirrors the repo convention of Dispatcher/lib/data.ts holding every mock
/// value in one file. No API calls exist anywhere in this app.

enum TripStatus { confirmed, booked, pending, cancelled }

class Trip {
  final String bookingId;
  final String from;
  final String to;
  final String date;
  final String time;
  final TripStatus status;
  final int ridersJoined;
  final int ridersMinimum;
  final String pickupPoint;
  final String pickupTime;
  final String dropoffPoint;
  final String dropoffTime;
  final String allowance;

  const Trip({
    required this.bookingId,
    required this.from,
    required this.to,
    required this.date,
    required this.time,
    required this.status,
    this.ridersJoined = 0,
    this.ridersMinimum = 4,
    this.pickupPoint = '',
    this.pickupTime = '',
    this.dropoffPoint = '',
    this.dropoffTime = '',
    this.allowance = '',
  });

  int get ridersNeeded => (ridersMinimum - ridersJoined).clamp(0, ridersMinimum);
}

class AvailableTrip {
  final String time;
  final String from;
  final String to;
  final int seatsLeft;
  final int price;
  final int maxPoints;

  const AvailableTrip({
    required this.time,
    required this.from,
    required this.to,
    required this.seatsLeft,
    required this.price,
    required this.maxPoints,
  });
}

class WalletActivity {
  final String label;
  final String date;
  final int points; // negative for redemptions / Gift-a-Seat donations
  final IconData icon;

  const WalletActivity({
    required this.label,
    required this.date,
    required this.points,
    required this.icon,
  });
}

class CommunityProject {
  final String title;
  final String blurb;
  final IconData icon;
  final int percent;
  final bool active;

  const CommunityProject({
    required this.title,
    required this.blurb,
    required this.icon,
    required this.percent,
    this.active = true,
  });
}

class Reward {
  final String name;
  final int points;
  final int quantity;
  final String category; // Travel | Merch | Gift Baskets | More
  final IconData icon;
  final List<String> includes;

  const Reward({
    required this.name,
    required this.points,
    required this.quantity,
    required this.category,
    required this.icon,
    this.includes = const [],
  });
}

class KeyPrinciple {
  final String title;
  final String detail;
  final IconData icon;

  const KeyPrinciple({required this.title, required this.detail, required this.icon});
}

// ---------------------------------------------------------------------------

const userName = 'Sarah J.';
const userFirstName = 'Sarah';
const userInitials = 'SJ';
const userLevel = 12;
const userLevelName = 'Northern Explorer';
const userPoints = 12450;
const pointsToNextLevel = 1250;
const inviteLink = 'https://northernlink.app/invite/Sarah25';
const inviteYouGet = 100;
const inviteTheyGet = 100;

const userStats = <(String, String)>[
  ('Trips Completed', '32'),
  ('Points Earned', '12,450'),
  ('Community Donations', '6,000 pts'),
  ('Friends Referred', '14'),
];

const nextTrip = Trip(
  bookingId: 'NL3421',
  from: 'Leaf Rapids',
  to: 'Thompson',
  date: 'Fri, Nov 22',
  time: '8:30 AM',
  status: TripStatus.confirmed,
  ridersJoined: 2,
  ridersMinimum: 4,
  pickupPoint: 'Leaf Rapids Co-op',
  pickupTime: '8:30 AM',
  dropoffPoint: 'Thompson – Northern Link Terminal',
  dropoffTime: '11:45 AM',
  allowance: '1 personal bag + 1 carry-on',
);

const upcomingTrips = <Trip>[
  nextTrip,
  Trip(
    bookingId: 'NL3510',
    from: 'Thompson',
    to: 'Leaf Rapids',
    date: 'Mon, Nov 25',
    time: '5:00 PM',
    status: TripStatus.booked,
  ),
  Trip(
    bookingId: 'NL3602',
    from: 'Leaf Rapids',
    to: 'Thompson',
    date: 'Fri, Nov 29',
    time: '8:30 AM',
    status: TripStatus.booked,
  ),
];

const availableTrips = <AvailableTrip>[
  AvailableTrip(
      time: '8:30 AM', from: 'Leaf Rapids', to: 'Thompson', seatsLeft: 4, price: 110, maxPoints: 160),
  AvailableTrip(
      time: '12:30 PM', from: 'Leaf Rapids', to: 'Thompson', seatsLeft: 7, price: 110, maxPoints: 120),
  AvailableTrip(
      time: '5:00 PM', from: 'Leaf Rapids', to: 'Thompson', seatsLeft: 3, price: 115, maxPoints: 160),
];

const walletActivity = <WalletActivity>[
  WalletActivity(
      label: 'Trip Completed', date: 'Nov 15, 2026', points: 150, icon: Icons.directions_bus_outlined),
  WalletActivity(label: 'Invite Bonus', date: 'Nov 14, 2026', points: 100, icon: Icons.emoji_events_outlined),
  // "Community Donation" on the board = the platform's Gift-a-Seat concept.
  WalletActivity(label: 'Community Donation', date: 'Nov 12, 2026', points: -500, icon: Icons.volunteer_activism_outlined),
  WalletActivity(
      label: 'Trip Completed', date: 'Nov 10, 2026', points: 130, icon: Icons.directions_bus_outlined),
];

const communityProjects = <CommunityProject>[
  CommunityProject(
    title: 'Winter Carnival 2026',
    blurb: 'Help bring our community together!',
    icon: Icons.ac_unit,
    percent: 72,
  ),
  CommunityProject(
    title: 'Leaf Rapids School Playground Fund',
    blurb: "Let's build a better place for our kids.",
    icon: Icons.park_outlined,
    percent: 50,
  ),
  CommunityProject(
    title: 'Youth Hockey Tournament',
    blurb: 'Support our local teams!',
    icon: Icons.emoji_events,
    percent: 91,
  ),
];

const rewards = <Reward>[
  Reward(name: 'Northern Link Hoodie', points: 2500, quantity: 10, category: 'Merch', icon: Icons.checkroom),
  Reward(
      name: 'Insulated Water Bottle',
      points: 1200,
      quantity: 28,
      category: 'Travel',
      icon: Icons.water_drop_outlined),
  Reward(name: 'Travel Mug', points: 1000, quantity: 35, category: 'Travel', icon: Icons.coffee_outlined),
  Reward(
    name: 'Gift Basket – Cozy Winter',
    points: 5000,
    quantity: 1,
    category: 'Gift Baskets',
    icon: Icons.card_giftcard,
    includes: [
      'Northern Link Toque',
      'Hot Chocolate Mix',
      'Travel Mug',
      'Handmade Soap',
      'Snacks & More!',
    ],
  ),
];

const rewardCategories = <String>['All', 'Travel', 'Merch', 'Gift Baskets', 'More'];

const keyPrinciples = <KeyPrinciple>[
  KeyPrinciple(
      title: 'No Cash Value', detail: 'Points cannot be redeemed for cash.', icon: Icons.money_off_outlined),
  KeyPrinciple(
      title: 'Non-Transferable', detail: 'Points are tied to your account.', icon: Icons.swap_horiz_outlined),
  KeyPrinciple(title: 'Non-Expiring', detail: 'Your points never expire.', icon: Icons.schedule_outlined),
  KeyPrinciple(
      title: 'Community First',
      detail: 'Support projects that matter to you.',
      icon: Icons.favorite_outline),
  KeyPrinciple(
      title: 'Minimum 4 Riders',
      detail: 'Helps keep routes running for everyone.',
      icon: Icons.groups_outlined),
];

String formatPoints(int pts) {
  final s = pts.abs().toString();
  final buf = StringBuffer();
  for (var i = 0; i < s.length; i++) {
    final fromEnd = s.length - i;
    buf.write(s[i]);
    if (fromEnd > 1 && (fromEnd - 1) % 3 == 0) buf.write(',');
  }
  return buf.toString();
}
