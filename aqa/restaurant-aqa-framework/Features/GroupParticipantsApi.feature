@api @level1 @level1api
Feature: Owner access to group pre-order participants

  Background:
    Given an isolated future group reservation for its Owner

  Rule: The participant list describes every guest cart accurately

    Scenario: A group without joined sessions has no participants or charges
      When the Owner requests the group participant list
      Then the group participant list is empty with no charges

    Scenario: The Owner sees confirmed draft and empty carts with exact amounts
      Given three guests have confirmed draft and empty carts
      When the Owner requests the group participant list
      Then every guest and selected dish is listed with the correct confirmation state
      And the guest cart amounts are 16.50 and 12.40 with an overall amount of 28.90

    Scenario: Every distinct dish in a guest cart is retained
      Given a guest has selected two different dishes
      When the Owner requests the group participant list
      Then both selected dishes appear once and their combined amount is 20.65

    Scenario: Changing a draft cart updates its amount without changing other carts
      Given three guests have confirmed draft and empty carts
      When the draft guest increases the selected dish quantity
      And the Owner requests the group participant list
      Then the draft cart has two dishes worth 24.80 and the overall amount is 41.30
      And the other guest carts retain their selections and confirmation states

  Rule: Only the reservation Owner may read the list

    @negative
    Scenario Outline: Another identity cannot read the Owner participant list
      Given an empty guest has joined the isolated group
      When <requester> requests the group participant list
      Then <requester> is refused without receiving any participant data

      Examples: Other identities
        | requester            |
        | another customer     |
        | a guest              |
        | a waiter             |
        | an anonymous visitor |

    @negative
    Scenario: The Owner is told when the requested group does not exist
      When the Owner requests a participant list for a group that does not exist
      Then the Owner is told the group was not found without receiving participant data
