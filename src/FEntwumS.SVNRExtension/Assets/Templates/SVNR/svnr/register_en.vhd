-- synchroner reset, synchrones laden
library IEEE;
use IEEE.STD_LOGIC_1164.all;
use IEEE.NUMERIC_STD.all;


entity register_en is
  generic (size        : integer := 1;
           clear_value : integer := 0);
  port (clock_t : in  std_logic;
        clr     : in  std_logic;
        load    : in  std_logic;
        data    : in  std_logic_vector (size-1 downto 0);
        q       : out std_logic_vector (size-1 downto 0));
end register_en;

architecture Behavioral of register_en is

begin

  process(clock_t, clr)
  begin

    if (rising_edge(clock_t)) then
      if(clr = '1') then
        q <= std_logic_vector(to_unsigned(clear_value, size));
      elsif (load = '1') then
        q <= data;
      end if;
    end if;

  end process;



end Behavioral;
