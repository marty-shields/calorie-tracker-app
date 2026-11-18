Feature: Food search favourites
  A food starred as a favourite is the first group in search, before the person types and while they type, and is not repeated under the other groups. App data is cleared before each scenario, so each one starts with no favourites and an empty diary.

  Scenario: A starred food is first in search, and unstarring it removes the group
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    And I have starred the food on Serving
    When I go back with the back arrow
    And I go back with the back arrow
    And I tap + on Lunch
    Then search lists only the Favourites group
    When I search for "oat"
    Then search lists the groups Favourites and Common, in that order
    And no food shows twice
    When I open the first food in the Favourites group
    And I tap the star on Serving
    Then the star on Serving is outlined and named "Add to favourites"
    When I go back with the back arrow
    Then search lists only the Common group
