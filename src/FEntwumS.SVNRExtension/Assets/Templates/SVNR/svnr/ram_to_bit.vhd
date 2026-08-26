library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

-- Speicher: 64 Words a 16 Bit KOdiert xxxx RRRR GGGG BBBB
-- ram to bit bekommt daten aus dem Ram und leitet diese an den 
-- WS2812 Treiber weiter

entity ram_to_bit is
  port (
    reset      : in  std_ulogic;
    clk        : in  std_ulogic;
    raddr      : out std_ulogic_vector(5 downto 0);  -- 64 Pixel
    dataRead   : in  std_ulogic_vector(15 downto 0);  -- xxxx rrrr gggg bbbb
    ws2812_out : out std_ulogic         -- Serieller Ausgang WS2812
    );
end ram_to_bit;


architecture behv of ram_to_bit is
  signal aserial_wnr            : std_ulogic                    := '0';
  signal data_in_s, aserial_run : std_ulogic;
  signal dataToLED              : std_ulogic_vector(23 downto 0);  -- xxxx rrrr gggg bbbb

  type STATE_TYPE is (state_wait_for_ws2812_timer,
                      state_wait_for_ws2812_start,
                      state_sfr_load,
                      state_acnt_inc,
                      state_aserial_run,
                      state_sfr_shift);
  signal state : STATE_TYPE := state_wait_for_ws2812_start;

  constant WAITTIMEREXP:integer := 5000;
  
  -- adresscounter

  signal acnt_rst : std_ulogic;
  signal acnt_inc : std_ulogic;

  signal waitstart : std_ulogic;
  signal waittimer : std_ulogic;

  signal register_shift : std_ulogic_vector(24 downto 0);
  signal sfr_load       : std_ulogic;
  signal sfr_done       : std_ulogic;
  signal acnt_eq63      : std_ulogic;
  signal sfr_shift      : std_ulogic := '0';

begin
  aserial : entity work.aserial
    port map(
      reset    => reset,
      clk      => clk,
      wnr      => aserial_wnr,
      data_in  => data_in_s,
      data_out => ws2812_out,
      run      => aserial_run);

  addrcnt : process(clk) is
    variable cnt : integer range 0 to 64;
  begin
    if rising_edge(clk) then
      if acnt_rst = '1' then
        cnt       := 0;
        acnt_eq63 <= '0';
      elsif cnt = 64 then
        acnt_eq63 <= '1';
      elsif acnt_inc = '1' then
        cnt := cnt + 1;

      end if;
    end if;
    raddr <= std_ulogic_vector(to_unsigned(cnt, 6));
  end process;

  -- signal dataToLED              : std_ulogic_vector(23 downto 0);  -- xxxx rrrr gggg bbbb
  -- 4 BIt intensität 0..15 pro Farbe, mit "0000" auffüllen, dann bleibt es dunkel
  dataToLED <= "0000" & dataRead(7 downto 4) &
               "0000" & dataRead(11 downto 8) &
               "0000" & dataRead(3 downto 0);

  shiftregister : process(clk) is
  begin
    if rising_edge(clk) then
      if sfr_load = '1' then
        register_shift <= dataToLED & '1';
        sfr_done       <= '0';
      elsif register_shift(23 downto 0) = "100000000000000000000000" then
        sfr_done <= '1';
      elsif sfr_shift = '1' then
        register_shift <= register_shift(23 downto 0) & '0';
      end if;
    end if;
  end process;
  -- Daten zum serialisierer

  data_in_s <= register_shift(24);
  --

  -- 50us Resettimer
  ws2812resettimer : process (clk) is
    variable cnt : integer range 0 to WAITTIMEREXP;
    variable run : std_logic := '0';
  begin
    if rising_edge(clk) then
      if run = '0' then
        cnt       := 0;
        waittimer <= '0';
        if waitstart = '1' then
          run := '1';
        end if;
      else                              --running
        if cnt = WAITTIMEREXP then
          cnt       := 0;
          waittimer <= '1';
          run       := '0';
        else
          cnt       := cnt + 1;
          waittimer <= '0';
          run       := '1';
        end if;
      end if;
    end if;
  end process ws2812resettimer;

  FSM : process(clk) is
  begin
    if rising_edge(clk) then
      case state is

        when state_wait_for_ws2812_start =>  --initialzustand
          state <= state_wait_for_ws2812_timer;

        when state_wait_for_ws2812_timer =>  -- warten auf Timer (Umschalten
                                             -- der ws2812)
          if waittimer = '1' then
            state       <= state_sfr_load;
            aserial_wnr <= '1';
          end if;
        when state_sfr_load =>               --Ram Laden
          state <= state_acnt_inc;
        when state_acnt_inc =>               -- adresse Hochzaehlen
          state <= state_aserial_run;
        when state_aserial_run =>
          if aserial_run = '0' then
            if sfr_done = '1' then
              if acnt_eq63 = '1' then
                state       <= state_wait_for_ws2812_start;
                aserial_wnr <= '0';
              else
                state <= state_sfr_load;
              end if;
            else
              state <= state_sfr_shift;
            end if;
          end if;
        when state_sfr_shift =>              --load bit
          state <= state_aserial_run;
      end case;
    end if;
  end process;

  waitstart <= '1' when state = state_wait_for_ws2812_start else '0';
  acnt_rst  <= '1' when state = state_wait_for_ws2812_start else '0';
  sfr_load  <= '1' when state = state_sfr_load        else '0';
  acnt_inc  <= '1' when state = state_acnt_inc        else '0';
  sfr_shift <= '1' when state = state_sfr_shift       else '0';




end architecture behv;
