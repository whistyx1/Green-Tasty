@api @level3 @level3api
Feature: Owner removes a guest from a group pre-order

  Background:
    Given an isolated future group reservation for level three

  Rule: Removal updates the group without changing other guests

    Scenario: Owner removes one choosing guest and the total is recalculated
      Given two guests have selected dishes in the group
      When the Owner removes the choosing guest
      Then only the confirmed guest remains with the correct total

    Scenario: Owner removes a guest who confirmed a cart
      Given two guests have selected dishes in the group
      When the Owner removes the confirmed guest
      Then only the choosing guest remains with the correct total

    Scenario: Owner removes the last guest
      Given one guest has confirmed a cart in the group
      When the Owner removes the confirmed guest
      Then the group has no guests and a zero total

    Scenario: A removed guest cannot add dishes again
      Given two guests have selected dishes in the group
      When the Owner removes the choosing guest
      And the removed guest tries to add another dish
      Then the old guest session is rejected and does not return to the group

  Rule: The Owner may remove only an existing guest of this open group

    @negative
    Scenario: A guest from another group cannot be removed
      Given another independent group has its own guest
      When the Owner tries to remove that guest from the first group
      Then removal is refused because the guest belongs to another group

    @negative
    Scenario: A nonexistent guest cannot be removed
      Given one guest has confirmed a cart in the group
      When the Owner tries to remove a guest who does not exist
      Then removal is refused because the guest does not exist

    @negative
    Scenario Outline: Only the Owner may remove a guest
      Given one guest has confirmed a cart in the group
      When <requester> tries to remove the guest
      Then <requester> cannot remove the guest and the group is unchanged

      Examples: Other identities
        | requester            |
        | another customer     |
        | a guest              |
        | a waiter             |
        | an anonymous visitor |
